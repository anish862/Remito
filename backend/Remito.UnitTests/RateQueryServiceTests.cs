using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Remito.Application.Abstractions;
using Remito.Domain.Entities;
using Remito.Infrastructure.Persistence;
using Remito.Infrastructure.Rates;

namespace Remito.UnitTests;

public sealed class RateQueryServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 4, 0, 0, TimeSpan.Zero);
    private static readonly Guid SourceId = Guid.Parse("77e86a4b-0f53-45fd-bba2-59a8b8bdb929");

    [Fact]
    public async Task Convert_uses_USD_cross_rate_and_returns_stale_flag()
    {
        await using var fixture = await CreateFixtureAsync();
        fixture.Context.RateSources.Add(NewSource());
        fixture.Context.MarketRates.AddRange(
            NewRate("OMR/USD", 0.385m, Now.AddMinutes(-2)),
            NewRate("USD/INR", 83m, Now.AddMinutes(-1)));
        await fixture.Context.SaveChangesAsync();
        var service = fixture.CreateService();

        var result = await service.ConvertAsync(10m, "OMR", "INR", default);

        Assert.Equal(31.955m, result.Rate);
        Assert.Equal(319.55m, result.Result);
        Assert.Equal(Now.AddMinutes(-2), result.Timestamp);
        Assert.False(result.IsStale);
    }

    [Theory]
    [InlineData("1.005", "1.00")]
    [InlineData("1.015", "1.02")]
    [InlineData("1.025", "1.02")]
    [InlineData("1.035", "1.04")]
    public async Task Convert_uses_bankers_rounding_at_destination_precision(string rateText, string expectedText)
    {
        await using var fixture = await CreateFixtureAsync();
        fixture.Context.RateSources.Add(NewSource());
        fixture.Context.MarketRates.Add(NewRate("OMR/INR", decimal.Parse(rateText), Now));
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.CreateService().ConvertAsync(1m, "OMR", "INR", default);

        Assert.Equal(decimal.Parse(expectedText), result.Result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1_000_000_000_001)]
    public async Task Convert_rejects_invalid_amounts(decimal amount)
    {
        await using var fixture = await CreateFixtureAsync();

        var exception = await Assert.ThrowsAsync<Remito.Application.Rates.RateQueryException>(
            () => fixture.CreateService().ConvertAsync(amount, "OMR", "INR", default));

        Assert.Equal(400, exception.StatusCode);
    }

    [Fact]
    public async Task Convert_rejects_same_currency_and_unknown_currency()
    {
        await using var fixture = await CreateFixtureAsync();
        var service = fixture.CreateService();

        var sameCurrency = await Assert.ThrowsAsync<Remito.Application.Rates.RateQueryException>(
            () => service.ConvertAsync(1m, "OMR", "OMR", default));
        var unknownCurrency = await Assert.ThrowsAsync<Remito.Application.Rates.RateQueryException>(
            () => service.ConvertAsync(1m, "OMR", "XYZ", default));

        Assert.Equal(400, sameCurrency.StatusCode);
        Assert.Equal(400, unknownCurrency.StatusCode);
    }

    [Fact]
    public async Task GetLatest_uses_cache_then_falls_back_to_SQL_and_marks_old_quotes_stale()
    {
        await using var fixture = await CreateFixtureAsync();
        fixture.Context.RateSources.Add(NewSource());
        fixture.Context.MarketRates.Add(NewRate("OMR/INR", 220m, Now.AddHours(-1)));
        await fixture.Context.SaveChangesAsync();
        var cache = new RecordingRateCache();
        var service = fixture.CreateService(cache);

        var fromSql = await service.GetLatestAsync("OMR", ["INR"], default);
        fixture.Context.MarketRates.RemoveRange(fixture.Context.MarketRates);
        await fixture.Context.SaveChangesAsync();
        var fromCache = await service.GetLatestAsync("OMR", ["INR"], default);

        Assert.True(fromSql.Rates.Single().IsStale);
        Assert.Equal(220m, fromSql.Rates.Single().Rate);
        Assert.Equal(fromSql.Base, fromCache.Base);
        Assert.Equal(fromSql.Rates.Single(), fromCache.Rates.Single());
        Assert.Contains("rates:latest:OMR-INR", cache.Entries.Keys);
    }

    [Fact]
    public async Task GetHistory_returns_summary_and_at_most_200_points()
    {
        await using var fixture = await CreateFixtureAsync();
        fixture.Context.RateHistories.AddRange(Enumerable.Range(0, 250).Select(index => new RateHistory
        {
            Id = Guid.NewGuid(),
            Pair = "OMR/INR",
            Bucket = HistoryBucket.OneDay,
            Timestamp = Now.AddMinutes(-249 + index),
            Value = index + 1
        }));
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.CreateService().GetHistoryAsync("OMR-INR", "1D", default);

        Assert.Equal(200, result.Points.Count);
        Assert.Equal(1m, result.Minimum);
        Assert.Equal(250m, result.Maximum);
        Assert.Equal(1m, result.Open);
        Assert.Equal(250m, result.Close);
        Assert.Equal(Now.AddMinutes(-249), result.Points[0].Timestamp);
        Assert.Equal(Now, result.Points[^1].Timestamp);
    }

    private static async Task<DatabaseFixture> CreateFixtureAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var context = new RemitoDbContext(
            new DbContextOptionsBuilder<RemitoDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        return new DatabaseFixture(connection, context);
    }

    private static RateSource NewSource() => new()
    {
        Id = SourceId,
        Name = "Query test provider",
        Type = RateSourceType.Provider,
        IsActive = true
    };

    private static MarketRate NewRate(string pair, decimal mid, DateTimeOffset fetchedAt) => new()
    {
        Id = Guid.NewGuid(),
        Pair = pair,
        Bid = mid,
        Ask = mid,
        Mid = mid,
        SourceId = SourceId,
        FetchedAt = fetchedAt,
        IsStale = false
    };

    private sealed class DatabaseFixture(SqliteConnection connection, RemitoDbContext context) : IAsyncDisposable
    {
        public RemitoDbContext Context { get; } = context;

        public RateQueryService CreateService(RecordingRateCache? cache = null) =>
            new(
                Context,
                cache ?? new RecordingRateCache(),
                [],
                new FixedTimeProvider(Now),
                NullLogger<RateQueryService>.Instance);

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class RecordingRateCache : IRateCache
    {
        public Dictionary<string, string> Entries { get; } = new(StringComparer.Ordinal);

        public ValueTask<string?> GetAsync(string key, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Entries.TryGetValue(key, out var value);
            return ValueTask.FromResult(value);
        }

        public ValueTask SetAsync(
            string key,
            string value,
            TimeSpan lifetime,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Entries[key] = value;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
