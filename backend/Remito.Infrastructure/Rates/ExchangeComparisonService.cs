using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Remito.Application.Abstractions;
using Remito.Application.Exchanges;
using Remito.Domain.Entities;
using Remito.Infrastructure.Persistence;

namespace Remito.Infrastructure.Rates;

public sealed class ExchangeComparisonService(
    RemitoDbContext dbContext,
    IRateCache rateCache,
    ILogger<ExchangeComparisonService> logger) : IExchangeComparisonService
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(60);
    private const decimal MaximumAmount = 1_000_000_000_000m;
    private const string DisclaimerText =
        "Remito does not process payments. Transactions are completed on the chosen licensed exchange.";

    public async Task<ExchangeComparisonResponse> GetExchangesAsync(
        string from,
        string to,
        decimal amount,
        ExchangeComparisonOptions options,
        CancellationToken cancellationToken)
    {
        var fromCode = NormalizeCurrency(from);
        var toCode = NormalizeCurrency(to);
        if (fromCode == toCode)
        {
            throw new ExchangeComparisonException(
                (int)HttpStatusCode.BadRequest,
                "Source and destination currencies must be different.");
        }

        if (amount <= 0 || amount > MaximumAmount)
        {
            throw new ExchangeComparisonException(
                (int)HttpStatusCode.BadRequest,
                $"Amount must be greater than zero and no more than {MaximumAmount.ToString(CultureInfo.InvariantCulture)}.");
        }

        ValidateOptions(options);
        var corridor = await dbContext.Corridors.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.FromCurrency == fromCode
                    && item.ToCurrency == toCode
                    && item.IsActive,
                cancellationToken);
        if (corridor is null)
        {
            throw new ExchangeComparisonException(
                (int)HttpStatusCode.NotFound,
                $"No active corridor is available for {fromCode}-{toCode}.");
        }

        var destinationPrecision = await dbContext.Currencies.AsNoTracking()
            .Where(currency => currency.Code == toCode)
            .Select(currency => (int?)currency.DecimalPlaces)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ExchangeComparisonException(
                (int)HttpStatusCode.BadRequest,
                $"Unknown currency code '{toCode}'.");
        var sourcePrecision = await dbContext.Currencies.AsNoTracking()
            .Where(currency => currency.Code == fromCode)
            .Select(currency => (int?)currency.DecimalPlaces)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ExchangeComparisonException(
                (int)HttpStatusCode.BadRequest,
                $"Unknown currency code '{fromCode}'.");

        var candidates = await GetCachedOrLoadAsync(
            corridor,
            amount,
            sourcePrecision,
            destinationPrecision,
            cancellationToken);

        var bestRate = candidates.Count == 0 ? (decimal?)null : candidates.Max(offer => offer.Rate);
        var ranked = candidates
            .Select(offer => offer with { IsBestRate = bestRate == offer.Rate })
            .Where(offer => options.HasApp is null || offer.HasApp == options.HasApp)
            .Where(offer => string.IsNullOrWhiteSpace(options.Search)
                || offer.Name.Contains(options.Search.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var ordered = Sort(ranked, options.Sort).ToArray();
        var skipCount = (int)Math.Min((long)(options.Page - 1) * options.PageSize, int.MaxValue);
        var pageItems = ordered
            .Skip(skipCount)
            .Take(options.PageSize)
            .ToArray();
        var organic = pageItems.Where(offer => !offer.IsSponsored).ToArray();
        var sponsored = pageItems.Where(offer => offer.IsSponsored).ToArray();

        return new ExchangeComparisonResponse(
            fromCode,
            toCode,
            amount,
            organic,
            sponsored.Length == 0 ? null : new SponsoredExchangeSlot("Sponsored", sponsored),
            DisclaimerText,
            options.Page,
            options.PageSize,
            ordered.Length);
    }

    private async Task<IReadOnlyList<ExchangeOffer>> GetCachedOrLoadAsync(
        Corridor corridor,
        decimal amount,
        int sourcePrecision,
        int destinationPrecision,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"exchanges:compare:{corridor.Id:N}:{amount.ToString("G29", CultureInfo.InvariantCulture)}";
        try
        {
            var cached = await rateCache.GetAsync(cacheKey, cancellationToken);
            if (cached is not null)
            {
                var offers = JsonSerializer.Deserialize<ExchangeOffer[]>(cached);
                if (offers is not null)
                {
                    return offers;
                }

                logger.LogWarning("Exchange comparison cache contained invalid data. CorridorId={CorridorId}", corridor.Id);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Exchange comparison cache read failed. CorridorId={CorridorId}", corridor.Id);
        }

        var offersFromDatabase = await LoadOffersAsync(
            corridor.Id,
            amount,
            sourcePrecision,
            destinationPrecision,
            cancellationToken);
        try
        {
            await rateCache.SetAsync(
                cacheKey,
                JsonSerializer.Serialize(offersFromDatabase),
                CacheLifetime,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Exchange comparison cache write failed. CorridorId={CorridorId}", corridor.Id);
        }

        return offersFromDatabase;
    }

    private async Task<IReadOnlyList<ExchangeOffer>> LoadOffersAsync(
        Guid corridorId,
        decimal amount,
        int sourcePrecision,
        int destinationPrecision,
        CancellationToken cancellationToken)
    {
        var quotes = await dbContext.ExchangeRates.AsNoTracking()
            .Where(rate => rate.CorridorId == corridorId
                && rate.Exchange.Status == ExchangeStatus.Active
                && !rate.Exchange.IsPlaceholder)
            .Select(rate => new
            {
                rate.ExchangeId,
                rate.Rate,
                rate.FetchedAt,
                rate.IsStale,
                rate.Exchange.Name,
                rate.Exchange.LogoUrl,
                rate.Exchange.HasApp,
                rate.Exchange.Tier
            })
            .ToListAsync(cancellationToken);
        var latestQuotes = quotes
            .GroupBy(quote => quote.ExchangeId)
            .Select(group => group.OrderByDescending(quote => quote.FetchedAt)
                .ThenByDescending(quote => quote.Rate)
                .First())
            .ToArray();
        if (latestQuotes.Length == 0)
        {
            return [];
        }

        var exchangeIds = latestQuotes.Select(quote => quote.ExchangeId).ToArray();
        var feeBands = await dbContext.ExchangeFees.AsNoTracking()
            .Where(fee => fee.CorridorId == corridorId && exchangeIds.Contains(fee.ExchangeId))
            .Select(fee => new FeeBand(
                fee.Id,
                fee.ExchangeId,
                fee.MinAmount,
                fee.MaxAmount,
                fee.FlatFee,
                fee.PercentFee))
            .ToListAsync(cancellationToken);

        return latestQuotes.Select(quote =>
        {
            var feeBand = feeBands
                .Where(fee => fee.ExchangeId == quote.ExchangeId
                    && (fee.MinAmount is null || amount >= fee.MinAmount)
                    && (fee.MaxAmount is null || amount <= fee.MaxAmount))
                .OrderByDescending(fee => fee.MinAmount ?? decimal.MinValue)
                .ThenBy(fee => fee.MaxAmount ?? decimal.MaxValue)
                .ThenBy(fee => fee.Id)
                .FirstOrDefault();
            decimal? calculatedFee = feeBand is not null
                && (feeBand.FlatFee is not null || feeBand.PercentFee is not null)
                    ? decimal.Round(
                        (feeBand.FlatFee ?? 0m) + amount * (feeBand.PercentFee ?? 0m) / 100m,
                        sourcePrecision,
                        MidpointRounding.ToEven)
                    : null;
            decimal? amountReceived = calculatedFee is null
                ? null
                : decimal.Round(
                    (amount - calculatedFee.Value) * quote.Rate,
                    destinationPrecision,
                    MidpointRounding.ToEven);

            return new ExchangeOffer(
                quote.ExchangeId,
                quote.Name,
                quote.LogoUrl,
                quote.Rate,
                calculatedFee,
                amountReceived,
                calculatedFee is null ? "Fee not provided" : string.Empty,
                quote.FetchedAt,
                quote.IsStale,
                quote.HasApp,
                quote.Tier == ExchangeTier.Sponsored,
                false);
        }).ToArray();
    }

    private static IEnumerable<ExchangeOffer> Sort(IReadOnlyList<ExchangeOffer> offers, string? sort) =>
        (sort ?? "best").Trim().ToLowerInvariant() switch
        {
            "best" => offers
                .OrderByDescending(offer => offer.AmountReceived is not null)
                .ThenByDescending(offer => offer.AmountReceived ?? offer.Rate)
                .ThenByDescending(offer => offer.Rate)
                .ThenBy(offer => offer.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(offer => offer.Name, StringComparer.Ordinal)
                .ThenBy(offer => offer.Id),
            "rate" => offers
                .OrderByDescending(offer => offer.Rate)
                .ThenBy(offer => offer.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(offer => offer.Name, StringComparer.Ordinal)
                .ThenBy(offer => offer.Id),
            "name" => offers
                .OrderBy(offer => offer.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(offer => offer.Name, StringComparer.Ordinal)
                .ThenBy(offer => offer.Id),
            _ => throw new ExchangeComparisonException(
                (int)HttpStatusCode.BadRequest,
                "Sort must be one of best, rate, or name.")
        };

    private static void ValidateOptions(ExchangeComparisonOptions options)
    {
        if (options.Page < 1 || options.PageSize is < 1 or > 100)
        {
            throw new ExchangeComparisonException(
                (int)HttpStatusCode.BadRequest,
                "Page must be positive and pageSize must be between 1 and 100.");
        }

        if (options.Sort is not null
            && !new[] { "best", "rate", "name" }.Contains(options.Sort.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            throw new ExchangeComparisonException(
                (int)HttpStatusCode.BadRequest,
                "Sort must be one of best, rate, or name.");
        }
    }

    private static string NormalizeCurrency(string code)
    {
        var normalized = code.Trim().ToUpperInvariant();
        if (normalized.Length != 3 || normalized.Any(character => character is < 'A' or > 'Z'))
        {
            throw new ExchangeComparisonException(
                (int)HttpStatusCode.BadRequest,
                $"Currency code '{code}' is invalid.");
        }

        return normalized;
    }

    private sealed record FeeBand(
        Guid Id,
        Guid ExchangeId,
        decimal? MinAmount,
        decimal? MaxAmount,
        decimal? FlatFee,
        decimal? PercentFee);
}
