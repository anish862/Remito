using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Remito.Application.Abstractions;
using Remito.Application.Rates;
using Remito.Domain.Entities;
using Remito.Infrastructure.Persistence;

namespace Remito.Infrastructure.Rates;

public sealed class RateQueryService(
    RemitoDbContext dbContext,
    IRateCache rateCache,
    IEnumerable<IRateProvider> providers,
    TimeProvider timeProvider,
    ILogger<RateQueryService> logger) : IRateQueryService
{
    private const int MaximumHistoryPoints = 200;
    private const decimal MaximumAmount = 1_000_000_000_000m;
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(24);
    private readonly Dictionary<Guid, TimeSpan> providerStaleness = providers
        .ToDictionary(provider => provider.SourceId, provider => provider.StalenessThreshold);

    public async Task<LatestRatesResponse> GetLatestAsync(
        string baseCurrency,
        IReadOnlyList<string> symbols,
        CancellationToken cancellationToken)
    {
        var baseCode = NormalizeCurrency(baseCurrency);
        var symbolCodes = symbols.Select(NormalizeCurrency).Distinct(StringComparer.Ordinal).ToArray();
        if (symbolCodes.Length == 0)
        {
            throw new RateQueryException((int)HttpStatusCode.BadRequest, "At least one symbol is required.");
        }

        await EnsureCurrenciesExistAsync([baseCode, .. symbolCodes], cancellationToken);
        var items = new List<LatestRateItem>(symbolCodes.Length);
        foreach (var symbol in symbolCodes)
        {
            var pair = $"{baseCode}/{symbol}";
            var snapshot = await GetLatestSnapshotAsync(pair, cancellationToken);
            if (snapshot is null)
            {
                throw new RateQueryException(
                    (int)HttpStatusCode.NotFound,
                    $"No rate is available for {baseCode}-{symbol}.");
            }

            var baseline = await GetDailyBaselineAsync(pair, cancellationToken);
            var change = baseline is > 0
                ? (snapshot.Mid - baseline.Value) / baseline.Value * 100m
                : 0m;
            items.Add(new LatestRateItem(
                symbol,
                snapshot.Mid,
                change,
                snapshot.FetchedAt,
                IsStale(snapshot)));
        }

        return new LatestRatesResponse(baseCode, items);
    }

    public async Task<RateHistoryResponse> GetHistoryAsync(
        string pair,
        string range,
        CancellationToken cancellationToken)
    {
        var codes = NormalizePair(pair);
        await EnsureCurrenciesExistAsync(codes, cancellationToken);
        var bucket = ParseRange(range);
        var normalizedPair = $"{codes[0]}/{codes[1]}";
        var now = timeProvider.GetUtcNow();
        var since = bucket switch
        {
            HistoryBucket.OneDay => now.AddDays(-1),
            HistoryBucket.OneWeek => now.AddDays(-7),
            HistoryBucket.OneMonth => now.AddMonths(-1),
            HistoryBucket.SixMonths => now.AddMonths(-6),
            HistoryBucket.OneYear => now.AddYears(-1),
            _ => throw new ArgumentOutOfRangeException(nameof(range))
        };

        var query = dbContext.RateHistories.AsNoTracking()
            .Where(point => point.Pair == normalizedPair && point.Bucket == bucket);
        List<RateHistory> history;
        if (dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            history = (await query.ToListAsync(cancellationToken))
                .Where(point => point.Timestamp >= since && point.Timestamp <= now)
                .ToList();
        }
        else
        {
            history = await query.Where(point => point.Timestamp >= since && point.Timestamp <= now)
                .ToListAsync(cancellationToken);
        }

        var ordered = history.OrderBy(point => point.Timestamp).ToArray();
        if (ordered.Length == 0)
        {
            throw new RateQueryException(
                (int)HttpStatusCode.NotFound,
                $"No {range} history is available for {codes[0]}-{codes[1]}.");
        }

        return new RateHistoryResponse(
            $"{codes[0]}-{codes[1]}",
            range,
            Downsample(ordered),
            ordered.Min(point => point.Value),
            ordered.Max(point => point.Value),
            ordered[0].Value,
            ordered[^1].Value);
    }

    public async Task<CurrencyConversionResponse> ConvertAsync(
        decimal amount,
        string fromCurrency,
        string toCurrency,
        CancellationToken cancellationToken)
    {
        if (amount <= 0 || amount > MaximumAmount)
        {
            throw new RateQueryException(
                (int)HttpStatusCode.BadRequest,
                $"Amount must be greater than zero and no more than {MaximumAmount.ToString(CultureInfo.InvariantCulture)}.");
        }

        var from = NormalizeCurrency(fromCurrency);
        var to = NormalizeCurrency(toCurrency);
        await EnsureCurrenciesExistAsync([from, to], cancellationToken);
        if (from == to)
        {
            throw new RateQueryException(
                (int)HttpStatusCode.BadRequest,
                "Source and destination currencies must be different.");
        }

        var currencies = await dbContext.Currencies.AsNoTracking()
            .Where(currency => currency.Code == to)
            .ToDictionaryAsync(currency => currency.Code, cancellationToken);
        var resolved = await ResolveConversionRateAsync(from, to, cancellationToken);
        if (resolved is null)
        {
            throw new RateQueryException((int)HttpStatusCode.NotFound, $"No rate is available for {from}-{to}.");
        }

        // Currency amounts use midpoint-to-even (banker's) rounding at the destination currency's precision.
        var result = decimal.Round(amount * resolved.Mid, currencies[to].DecimalPlaces, MidpointRounding.ToEven);
        return new CurrencyConversionResponse(
            amount,
            from,
            to,
            result,
            resolved.Mid,
            resolved.FetchedAt,
            resolved.IsStale);
    }

    private async Task<RateSnapshot?> ResolveConversionRateAsync(
        string from,
        string to,
        CancellationToken cancellationToken)
    {
        var direct = await GetRateInDirectionAsync(from, to, cancellationToken);
        if (direct is not null)
        {
            return direct;
        }

        if (from == "USD" || to == "USD")
        {
            return null;
        }

        var fromUsd = await GetRateInDirectionAsync(from, "USD", cancellationToken);
        var usdTo = await GetRateInDirectionAsync("USD", to, cancellationToken);
        if (fromUsd is null || usdTo is null)
        {
            return null;
        }

        return new RateSnapshot(
            fromUsd.Mid * usdTo.Mid,
            MinTimestamp(fromUsd.FetchedAt, usdTo.FetchedAt),
            fromUsd.IsStale || usdTo.IsStale,
            TimeSpan.Zero);
    }

    private async Task<RateSnapshot?> GetRateInDirectionAsync(
        string from,
        string to,
        CancellationToken cancellationToken)
    {
        var direct = await GetLatestSnapshotAsync($"{from}/{to}", cancellationToken);
        if (direct is not null)
        {
            return direct;
        }

        var inverse = await GetLatestSnapshotAsync($"{to}/{from}", cancellationToken);
        return inverse is null
            ? null
            : inverse with { Mid = 1m / inverse.Mid };
    }

    private async Task<RateSnapshot?> GetLatestSnapshotAsync(string pair, CancellationToken cancellationToken)
    {
        var cacheKey = $"rates:latest:{pair.Replace("/", "-", StringComparison.Ordinal)}";
        try
        {
            var cachedValue = await rateCache.GetAsync(cacheKey, cancellationToken);
            if (cachedValue is not null)
            {
                var cached = JsonSerializer.Deserialize<CachedRate>(cachedValue);
                if (cached is not null && cached.Pair == pair && cached.Mid > 0)
                {
                    var threshold = TimeSpan.FromMinutes(cached.StalenessMinutes);
                    return new RateSnapshot(
                        cached.Mid,
                        cached.FetchedAt,
                        cached.IsStale || timeProvider.GetUtcNow() - cached.FetchedAt > threshold,
                        threshold);
                }

                logger.LogWarning("Cached rate payload was invalid and will be reloaded from SQL. Pair={Pair}", pair);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Rate cache read failed; falling back to SQL. Pair={Pair}", pair);
        }

        var sourceRates = await dbContext.MarketRates.AsNoTracking()
            .Include(rate => rate.Source)
            .Where(rate => rate.Pair == pair)
            .ToListAsync(cancellationToken);
        var latest = sourceRates
            .GroupBy(rate => rate.SourceId)
            .Select(group => group.MaxBy(rate => rate.FetchedAt)!)
            .OrderBy(rate => rate.IsStale)
            .ThenBy(rate => rate.Source.Priority)
            .ThenByDescending(rate => rate.FetchedAt)
            .FirstOrDefault();
        if (latest is null)
        {
            return null;
        }

        var staleAfter = providerStaleness.TryGetValue(latest.SourceId, out var configuredThreshold)
            ? configuredThreshold
            : latest.Source.Type == RateSourceType.Provider
                ? TimeSpan.FromMinutes(15)
                : TimeSpan.FromMinutes(60);
        var isStale = latest.IsStale || timeProvider.GetUtcNow() - latest.FetchedAt > staleAfter;
        var response = new RateSnapshot(latest.Mid, latest.FetchedAt, isStale, staleAfter);
        try
        {
            await rateCache.SetAsync(
                cacheKey,
                JsonSerializer.Serialize(new CachedRate(
                    pair,
                    latest.Bid,
                    latest.Ask,
                    latest.Mid,
                    latest.SourceId,
                    latest.FetchedAt,
                    isStale,
                    (int)staleAfter.TotalMinutes)),
                CacheLifetime,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Rate cache write failed; SQL result will be returned. Pair={Pair}", pair);
        }

        return response;
    }

    private async Task<decimal?> GetDailyBaselineAsync(string pair, CancellationToken cancellationToken)
    {
        var points = await dbContext.RateHistories.AsNoTracking()
            .Where(point => point.Pair == pair && point.Bucket == HistoryBucket.OneDay)
            .ToListAsync(cancellationToken);
        var earliest = points
            .Where(point => point.Timestamp >= timeProvider.GetUtcNow().AddDays(-1))
            .MinBy(point => point.Timestamp);
        return earliest?.Value;
    }

    private async Task EnsureCurrenciesExistAsync(
        IReadOnlyCollection<string> codes,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.Currencies.AsNoTracking()
            .Where(currency => codes.Contains(currency.Code))
            .Select(currency => currency.Code)
            .ToListAsync(cancellationToken);
        var unknown = codes.Except(existing, StringComparer.Ordinal).FirstOrDefault();
        if (unknown is not null)
        {
            throw new RateQueryException((int)HttpStatusCode.BadRequest, $"Unknown currency code '{unknown}'.");
        }
    }

    private static IReadOnlyList<RateHistoryPoint> Downsample(IReadOnlyList<RateHistory> points)
    {
        if (points.Count <= MaximumHistoryPoints)
        {
            return points.Select(point => new RateHistoryPoint(point.Timestamp, point.Value)).ToArray();
        }

        var sampled = new List<RateHistoryPoint>(MaximumHistoryPoints);
        for (var index = 0; index < MaximumHistoryPoints; index++)
        {
            var sourceIndex = (int)((long)index * (points.Count - 1) / (MaximumHistoryPoints - 1));
            sampled.Add(new RateHistoryPoint(points[sourceIndex].Timestamp, points[sourceIndex].Value));
        }

        return sampled;
    }

    private static string NormalizeCurrency(string code)
    {
        var normalized = code.Trim().ToUpperInvariant();
        if (normalized.Length != 3 || normalized.Any(character => character is < 'A' or > 'Z'))
        {
            throw new RateQueryException((int)HttpStatusCode.BadRequest, $"Currency code '{code}' is invalid.");
        }

        return normalized;
    }

    private static string[] NormalizePair(string pair)
    {
        var parts = pair.Trim().ToUpperInvariant().Split(['-', '/'], StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            throw new RateQueryException((int)HttpStatusCode.BadRequest, "Pair must use BASE-QUOTE format.");
        }

        return [NormalizeCurrency(parts[0]), NormalizeCurrency(parts[1])];
    }

    private static HistoryBucket ParseRange(string range) =>
        range.Trim().ToUpperInvariant() switch
        {
            "1D" => HistoryBucket.OneDay,
            "1W" => HistoryBucket.OneWeek,
            "1M" => HistoryBucket.OneMonth,
            "6M" => HistoryBucket.SixMonths,
            "1Y" => HistoryBucket.OneYear,
            _ => throw new RateQueryException(
                (int)HttpStatusCode.BadRequest,
                "Range must be one of 1D, 1W, 1M, 6M, or 1Y.")
        };

    private bool IsStale(RateSnapshot snapshot) =>
        snapshot.IsStale
        || snapshot.StalenessThreshold > TimeSpan.Zero
            && timeProvider.GetUtcNow() - snapshot.FetchedAt > snapshot.StalenessThreshold;

    private static DateTimeOffset MinTimestamp(DateTimeOffset left, DateTimeOffset right) =>
        left < right ? left : right;

    private sealed record RateSnapshot(
        decimal Mid,
        DateTimeOffset FetchedAt,
        bool IsStale,
        TimeSpan StalenessThreshold);

    private sealed record CachedRate(
        string Pair,
        decimal Bid,
        decimal Ask,
        decimal Mid,
        Guid SourceId,
        DateTimeOffset FetchedAt,
        bool IsStale,
        int StalenessMinutes);
}
