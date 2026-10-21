using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;
using Remito.Application.Abstractions;
using Remito.Domain.Entities;
using Remito.Infrastructure.Persistence;

namespace Remito.Infrastructure.Rates;

public sealed class RateIngestionJob(
    IEnumerable<IRateProvider> providers,
    RemitoDbContext dbContext,
    IRateCache rateCache,
    IOptions<RateGuardOptions> guardOptions,
    TimeProvider timeProvider,
    RatePipelineMetrics metrics,
    ILogger<RateIngestionJob> logger) : IRateIngestionJob
{
    private static readonly ConcurrentDictionary<Guid, ResiliencePipeline> ResilienceBySource = new();

    private static ResiliencePipeline CreateResiliencePipeline() =>
        new ResiliencePipelineBuilder()
            .AddRetry(new Polly.Retry.RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(300),
                BackoffType = Polly.DelayBackoffType.Exponential,
                UseJitter = true
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 4,
                BreakDuration = TimeSpan.FromSeconds(30)
            })
            .AddTimeout(TimeSpan.FromSeconds(30))
            .Build();

    private readonly RateGuardOptions guards = guardOptions.Value;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        foreach (var provider in providers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stopwatch = Stopwatch.StartNew();
            IReadOnlyList<RateQuote> quotes;
            try
            {
                quotes = [];
                await ResilienceBySource.GetOrAdd(provider.SourceId, _ => CreateResiliencePipeline())
                    .ExecuteAsync(
                    async token =>
                    {
                        quotes = await provider.FetchAsync(token);
                    },
                    cancellationToken);
                if (quotes.Count == 0)
                {
                    throw new InvalidDataException($"Rate source '{provider.SourceName}' returned no quotes.");
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                await MarkSourceStaleAsync(provider, cancellationToken);
                metrics.RecordFetch(provider.SourceName, stopwatch.Elapsed, succeeded: false);
                logger.LogError(
                    exception,
                    "Rate source fetch failed; last good rates were retained and marked stale. Source={RateSource}",
                    provider.SourceName);
                continue;
            }

            try
            {
                await StoreQuotesAsync(provider, quotes, cancellationToken);
            }
            catch (Exception exception) when (exception is InvalidDataException or FormatException)
            {
                await MarkSourceStaleAsync(provider, cancellationToken);
                metrics.RecordFetch(provider.SourceName, stopwatch.Elapsed, succeeded: false);
                logger.LogError(
                    exception,
                    "Rate source returned no usable data; last good rates were retained and marked stale. Source={RateSource}",
                    provider.SourceName);
                continue;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                metrics.RecordFetch(provider.SourceName, stopwatch.Elapsed, succeeded: false);
                logger.LogError(
                    exception,
                    "Rate source data could not be persisted. Source={RateSource}",
                    provider.SourceName);
                throw;
            }

            metrics.RecordFetch(provider.SourceName, stopwatch.Elapsed, succeeded: true);
            logger.LogInformation(
                "Rate source fetch completed. Source={RateSource} QuoteCount={QuoteCount} DurationMs={DurationMs}",
                provider.SourceName,
                quotes.Count,
                stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    private async Task StoreQuotesAsync(
        IRateProvider provider,
        IReadOnlyList<RateQuote> quotes,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await EnsureSourceAsync(provider, cancellationToken);
        var cacheUpdates = new List<MarketRate>();
        var validQuoteCount = 0;

        foreach (var quote in quotes)
        {
            if (quote.SourceId != provider.SourceId)
            {
                throw new InvalidDataException($"Rate source '{provider.SourceName}' returned a quote with a different source ID.");
            }

            string pair;
            try
            {
                pair = RateNormalizer.NormalizePair(quote.Pair);
                ValidateQuote(quote, pair);
            }
            catch (Exception exception) when (exception is FormatException or InvalidDataException)
            {
                logger.LogWarning(
                    exception,
                    "Rate quote failed normalization or sanity validation. Source={RateSource} Pair={Pair}",
                    provider.SourceName,
                    quote.Pair);
                continue;
            }

            var normalizedQuote = quote with
            {
                Pair = pair,
                Mid = (quote.Bid + quote.Ask) / 2m
            };
            validQuoteCount++;
            var latest = dbContext.MarketRates.Local
                .Where(rate => rate.SourceId == provider.SourceId && rate.Pair == pair)
                .OrderByDescending(rate => rate.FetchedAt)
                .FirstOrDefault()
                ?? (await dbContext.MarketRates
                    .Where(rate => rate.SourceId == provider.SourceId && rate.Pair == pair)
                    .ToListAsync(cancellationToken))
                    .MaxBy(rate => rate.FetchedAt);

            if (now - normalizedQuote.FetchedAt > provider.StalenessThreshold
                || normalizedQuote.FetchedAt > now.AddMinutes(1))
            {
                if (latest is not null)
                {
                    latest.IsStale = true;
                }

                logger.LogWarning(
                    "Rate quote exceeded its source freshness window and was not stored. Source={RateSource} Pair={Pair} FetchedAt={FetchedAt}",
                    provider.SourceName,
                    pair,
                    quote.FetchedAt);
                continue;
            }

            var lastGood = dbContext.MarketRates.Local
                .Where(rate => rate.SourceId == provider.SourceId && rate.Pair == pair && !rate.IsStale)
                .OrderByDescending(rate => rate.FetchedAt)
                .FirstOrDefault()
                ?? (await dbContext.MarketRates
                    .Where(rate => rate.SourceId == provider.SourceId && rate.Pair == pair && !rate.IsStale)
                    .ToListAsync(cancellationToken))
                    .MaxBy(rate => rate.FetchedAt);
            if (lastGood is not null && IsVarianceExceeded(lastGood.Mid, normalizedQuote.Mid))
            {
                var variance = Math.Abs((normalizedQuote.Mid - lastGood.Mid) / lastGood.Mid) * 100m;
                dbContext.RateQuarantines.Add(new RateQuarantine
                {
                    Id = Guid.NewGuid(),
                    Pair = pair,
                    SourceId = provider.SourceId,
                    Bid = normalizedQuote.Bid,
                    Ask = normalizedQuote.Ask,
                    Mid = normalizedQuote.Mid,
                    FetchedAt = normalizedQuote.FetchedAt,
                    QuarantinedAt = now,
                    Reason = $"Variance {variance.ToString("F2", CultureInfo.InvariantCulture)}% exceeded {guards.MaximumVariancePercent}%."
                });
                metrics.RecordQuarantine(provider.SourceName, pair);
                logger.LogWarning(
                    "Rate quote quarantined after a variance guard alert. Source={RateSource} Pair={Pair} VariancePercent={VariancePercent} ThresholdPercent={ThresholdPercent}",
                    provider.SourceName,
                    pair,
                    variance,
                    guards.MaximumVariancePercent);
                continue;
            }

            if (latest is not null
                && latest.Bid == normalizedQuote.Bid
                && latest.Ask == normalizedQuote.Ask
                && latest.Mid == normalizedQuote.Mid)
            {
                latest.FetchedAt = normalizedQuote.FetchedAt;
                latest.IsStale = false;
                cacheUpdates.Add(latest);
                metrics.RecordFreshness(provider.SourceName, now - normalizedQuote.FetchedAt);
                continue;
            }

            var marketRate = new MarketRate
            {
                Id = Guid.NewGuid(),
                Pair = pair,
                Bid = normalizedQuote.Bid,
                Ask = normalizedQuote.Ask,
                Mid = normalizedQuote.Mid,
                SourceId = provider.SourceId,
                FetchedAt = normalizedQuote.FetchedAt,
                IsStale = false
            };
            dbContext.MarketRates.Add(marketRate);
            cacheUpdates.Add(marketRate);
            metrics.RecordFreshness(provider.SourceName, now - normalizedQuote.FetchedAt);
        }

        if (validQuoteCount == 0)
        {
            throw new InvalidDataException($"Rate source '{provider.SourceName}' returned no valid quotes.");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        foreach (var rate in cacheUpdates)
        {
            try
            {
                var payload = JsonSerializer.Serialize(new CachedRate(
                    rate.Pair,
                    rate.Bid,
                    rate.Ask,
                    rate.Mid,
                    rate.SourceId,
                    rate.FetchedAt,
                    rate.IsStale));
                await rateCache.SetAsync(
                    $"rates:latest:{rate.Pair.Replace("/", "-", StringComparison.Ordinal)}:{rate.SourceId:N}",
                    payload,
                    provider.StalenessThreshold,
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(
                    exception,
                    "Latest rate persisted to SQL but Redis cache update failed. Source={RateSource} Pair={Pair}",
                    provider.SourceName,
                    rate.Pair);
            }
        }
    }

    private void ValidateQuote(RateQuote quote, string pair)
    {
        if (quote.Bid <= 0 || quote.Ask < quote.Bid)
        {
            throw new InvalidDataException("Rate quote must have a positive bid no greater than ask.");
        }

        if (guards.PairBounds.TryGetValue(pair, out var bounds)
            && (bounds.Minimum <= 0 || bounds.Maximum <= bounds.Minimum
                || (quote.Bid + quote.Ask) / 2m < bounds.Minimum
                || (quote.Bid + quote.Ask) / 2m > bounds.Maximum))
        {
            throw new InvalidDataException($"Rate quote is outside configured sanity bounds for {pair}.");
        }
    }

    private bool IsVarianceExceeded(decimal previous, decimal current) =>
        previous > 0
        && guards.MaximumVariancePercent > 0
        && Math.Abs((current - previous) / previous) * 100m > guards.MaximumVariancePercent;

    private async Task EnsureSourceAsync(IRateProvider provider, CancellationToken cancellationToken)
    {
        var source = await dbContext.RateSources
            .FirstOrDefaultAsync(candidate => candidate.Id == provider.SourceId, cancellationToken);
        if (source is null)
        {
            dbContext.RateSources.Add(new RateSource
            {
                Id = provider.SourceId,
                Name = provider.SourceName,
                Type = provider.SourceType,
                Priority = 0,
                IsActive = true
            });
        }
        else if (source.Name != provider.SourceName || source.Type != provider.SourceType)
        {
            throw new InvalidOperationException(
                $"Configured rate source {provider.SourceId} conflicts with the registered source identity.");
        }
    }

    private async Task MarkSourceStaleAsync(IRateProvider provider, CancellationToken cancellationToken)
    {
        try
        {
            var rates = await dbContext.MarketRates
                .Where(rate => rate.SourceId == provider.SourceId && !rate.IsStale)
                .ToListAsync(cancellationToken);
            var latestRates = rates
                .GroupBy(rate => rate.Pair)
                .Select(group => group.MaxBy(rate => rate.FetchedAt)!)
                .ToArray();
            foreach (var rate in latestRates)
            {
                rate.IsStale = true;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            foreach (var rate in latestRates)
            {
                metrics.RecordFreshness(provider.SourceName, timeProvider.GetUtcNow() - rate.FetchedAt);
                try
                {
                    var payload = JsonSerializer.Serialize(new CachedRate(
                        rate.Pair,
                        rate.Bid,
                        rate.Ask,
                        rate.Mid,
                        rate.SourceId,
                        rate.FetchedAt,
                        IsStale: true));
                    await rateCache.SetAsync(
                        $"rates:latest:{rate.Pair.Replace("/", "-", StringComparison.Ordinal)}:{rate.SourceId:N}",
                        payload,
                        provider.StalenessThreshold,
                        cancellationToken);
                }
                catch (Exception cacheException) when (cacheException is not OperationCanceledException)
                {
                    logger.LogError(
                        cacheException,
                        "Could not mark the cached rate stale after a provider outage. Source={RateSource} Pair={Pair}",
                        provider.SourceName,
                        rate.Pair);
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                exception,
                "Could not mark retained rates stale after a provider outage. Source={RateSource}",
                provider.SourceName);
        }
    }

    private sealed record CachedRate(
        string Pair,
        decimal Bid,
        decimal Ask,
        decimal Mid,
        Guid SourceId,
        DateTimeOffset FetchedAt,
        bool IsStale);
}
