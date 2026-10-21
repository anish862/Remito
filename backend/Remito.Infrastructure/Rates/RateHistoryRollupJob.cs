using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Remito.Domain.Entities;
using Remito.Infrastructure.Persistence;

namespace Remito.Infrastructure.Rates;

public sealed class RateHistoryRollupJob(
    RemitoDbContext dbContext,
    IOptions<RateGuardOptions> guardOptions,
    TimeProvider timeProvider,
    ILogger<RateHistoryRollupJob> logger) : IRateHistoryRollupJob
{
    private static readonly (HistoryBucket Bucket, RollupResolution Resolution)[] Resolutions =
    [
        (HistoryBucket.OneDay, RollupResolution.FiveMinutes),
        (HistoryBucket.OneWeek, RollupResolution.OneHour),
        (HistoryBucket.OneMonth, RollupResolution.OneDay),
        (HistoryBucket.SixMonths, RollupResolution.OneWeek),
        (HistoryBucket.OneYear, RollupResolution.OneMonth)
    ];

    private readonly RateGuardOptions guards = guardOptions.Value;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var earliest = now.AddDays(-Math.Min(guards.HistoryRetentionDays, 400));
        var isSqlite = dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
        var ratesQuery = dbContext.MarketRates.AsNoTracking().Where(rate => !rate.IsStale);
        if (!isSqlite)
        {
            ratesQuery = ratesQuery.Where(rate => rate.FetchedAt >= earliest && rate.FetchedAt <= now);
        }

        var rates = await ratesQuery.ToListAsync(cancellationToken);
        if (isSqlite)
        {
            rates = rates.Where(rate => rate.FetchedAt >= earliest && rate.FetchedAt <= now).ToList();
        }
        var upsertCount = 0;

        foreach (var (bucket, resolution) in Resolutions)
        {
            var from = WindowStart(bucket, now);
            var points = rates
                .Where(rate => rate.FetchedAt >= from)
                .GroupBy(rate => (rate.Pair, Timestamp: Floor(resolution, rate.FetchedAt)))
                .Select(group => new
                {
                    group.Key.Pair,
                    group.Key.Timestamp,
                    Rate = group.OrderByDescending(rate => rate.FetchedAt).First()
                })
                .ToArray();

            foreach (var point in points)
            {
                var existing = await dbContext.RateHistories.FirstOrDefaultAsync(
                    history => history.Pair == point.Pair
                        && history.Bucket == bucket
                        && history.Timestamp == point.Timestamp,
                    cancellationToken);
                if (existing is null)
                {
                    dbContext.RateHistories.Add(new RateHistory
                    {
                        Id = Guid.NewGuid(),
                        Pair = point.Pair,
                        Bucket = bucket,
                        Timestamp = point.Timestamp,
                        Value = point.Rate.Mid
                    });
                }
                else
                {
                    existing.Value = point.Rate.Mid;
                }

                upsertCount++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Rate history roll-up completed. PointCount={PointCount} SourceRateCount={SourceRateCount}",
            upsertCount,
            rates.Count);
    }

    private enum RollupResolution
    {
        FiveMinutes,
        OneHour,
        OneDay,
        OneWeek,
        OneMonth
    }

    private static DateTimeOffset Floor(RollupResolution resolution, DateTimeOffset timestamp)
    {
        var utc = timestamp.ToUniversalTime();
        var floored = resolution switch
        {
            RollupResolution.FiveMinutes => new DateTimeOffset(
                utc.Year,
                utc.Month,
                utc.Day,
                utc.Hour,
                utc.Minute / 5 * 5,
                0,
                TimeSpan.Zero),
            RollupResolution.OneHour => new DateTimeOffset(
                utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, TimeSpan.Zero),
            RollupResolution.OneDay => new DateTimeOffset(
                utc.Year, utc.Month, utc.Day, 0, 0, 0, TimeSpan.Zero),
            RollupResolution.OneWeek => FloorWeek(utc),
            RollupResolution.OneMonth => new DateTimeOffset(
                utc.Year, utc.Month, 1, 0, 0, 0, TimeSpan.Zero),
            _ => throw new ArgumentOutOfRangeException(nameof(resolution), resolution, null)
        };
        return floored;
    }

    private static DateTimeOffset FloorWeek(DateTimeOffset utc)
    {
        var daysSinceMonday = ((int)utc.DayOfWeek + 6) % 7;
        return new DateTimeOffset(utc.Date.AddDays(-daysSinceMonday), TimeSpan.Zero);
    }

    private static DateTimeOffset WindowStart(HistoryBucket bucket, DateTimeOffset now) =>
        bucket switch
        {
            HistoryBucket.OneDay => now.AddDays(-1),
            HistoryBucket.OneWeek => now.AddDays(-7),
            HistoryBucket.OneMonth => now.AddMonths(-1),
            HistoryBucket.SixMonths => now.AddMonths(-6),
            HistoryBucket.OneYear => now.AddYears(-1),
            _ => throw new ArgumentOutOfRangeException(nameof(bucket), bucket, null)
        };
}
