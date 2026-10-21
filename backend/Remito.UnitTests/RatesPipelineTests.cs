using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.ApplicationInsights;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.ApplicationInsights.Extensibility;
using Remito.Domain.Entities;
using Remito.Infrastructure.Persistence;
using Remito.Infrastructure.Rates;

namespace Remito.UnitTests;

public sealed class RatesPipelineTests
{
    private static readonly Guid SourceId = Guid.Parse("0a9f1602-76cb-4f31-897c-9a9bd0c7460e");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-21T12:00:00Z");

    [Fact]
    public async Task Market_provider_parses_quotes_and_normalizes_midpoint()
    {
        var handler = new RecordingHandler(
            """{"quotes":[{"pair":"usd-inr","bid":83.95,"ask":84.05}]}""");
        using var httpClient = new HttpClient(handler);
        var provider = new MarketRateProvider(
            httpClient,
            Options.Create(new MarketRateProviderOptions
            {
                Endpoint = "https://market.example/rates",
                SourceId = SourceId,
                ApiKey = "fixture-key"
            }),
            new FixedTimeProvider(Now));

        var quotes = await provider.FetchAsync(default);

        quotes.Should().ContainSingle().Which.Should().BeEquivalentTo(new RateQuote(
            "USD/INR",
            83.95m,
            84.05m,
            84m,
            SourceId,
            Now));
        handler.LastRequest!.Headers.GetValues("Authorization").Should().Contain("Bearer fixture-key");
    }

    [Fact]
    public async Task Development_fixture_provider_reads_the_worker_recording()
    {
        var provider = new FixtureRateProvider(
            Options.Create(new FixtureRateProviderOptions
            {
                Enabled = true,
                SourceId = SourceId,
                Path = "Fixtures/rates.json"
            }),
            new FixedTimeProvider(Now));

        var quotes = await provider.FetchAsync(default);

        quotes.Should().HaveCount(3);
        quotes.Should().ContainSingle(quote => quote.Pair == "USD/INR" && quote.Mid == 84m);
    }

    [Theory]
    [InlineData("json", """[{"pair":"USD/INR","bid":83,"ask":85,"mid":84}]""")]
    [InlineData("csv", "pair,bid,ask,mid\nUSD/INR,83,85,84")]
    [InlineData("html", "<table><tr><th>pair</th><th>bid</th><th>ask</th><th>mid</th></tr><tr><td>USD/INR</td><td>83</td><td>85</td><td>84</td></tr></table>")]
    public async Task Exchange_feed_strategies_parse_recorded_fixtures(string strategy, string fixture)
    {
        using var httpClient = new HttpClient(new RecordingHandler(fixture));
        var provider = new ExchangeFeedProvider(
            new FixedHttpClientFactory(httpClient),
            new ExchangeFeedDefinition
            {
                Name = "Fixture exchange",
                Endpoint = "https://exchange.example/rates",
                SourceId = SourceId,
                Strategy = strategy
            },
            new FixedTimeProvider(Now));

        var quotes = await provider.FetchAsync(default);

        quotes.Should().ContainSingle().Which.Should().BeEquivalentTo(new RateQuote(
            "USD/INR", 83m, 85m, 84m, SourceId, Now));
    }

    [Theory]
    [InlineData(92.39, false)]
    [InlineData(92.40, false)]
    [InlineData(92.41, true)]
    public async Task Variance_guard_quarantines_only_quotes_above_threshold(decimal newMid, bool quarantined)
    {
        await using var fixture = await CreateDatabaseAsync();
        var provider = new StaticRateProvider(
            SourceId,
            [Quote(84m, Now.AddMinutes(-1)), Quote(newMid, Now)]);
        var job = CreateIngestionJob(fixture.DbContext, [provider]);

        await job.RunAsync(default);

        (await fixture.DbContext.RateQuarantines.CountAsync()).Should().Be(quarantined ? 1 : 0);
        (await fixture.DbContext.MarketRates.CountAsync()).Should().Be(quarantined ? 1 : 2);
        if (quarantined)
        {
            (await fixture.DbContext.RateQuarantines.SingleAsync()).Reason.Should().Contain("Variance");
        }
    }

    [Fact]
    public async Task Failed_source_marks_last_good_rate_stale_and_keeps_its_value()
    {
        await using var fixture = await CreateDatabaseAsync();
        fixture.DbContext.RateSources.Add(NewSource());
        fixture.DbContext.MarketRates.Add(new MarketRate
        {
            Id = Guid.NewGuid(),
            Pair = "USD/INR",
            Bid = 83.95m,
            Ask = 84.05m,
            Mid = 84m,
            SourceId = SourceId,
            FetchedAt = Now.AddMinutes(-2)
        });
        await fixture.DbContext.SaveChangesAsync();
        var cache = new RecordingRateCache();
        var job = CreateIngestionJob(
            fixture.DbContext,
            [new FailingRateProvider(SourceId)],
            cache);

        await job.RunAsync(default);

        var storedRate = await fixture.DbContext.MarketRates.SingleAsync();
        storedRate.Mid.Should().Be(84m);
        storedRate.IsStale.Should().BeTrue();
        using var cached = System.Text.Json.JsonDocument.Parse(cache.LastValue!);
        cached.RootElement.GetProperty("IsStale").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Failed_provider_does_not_prevent_an_independent_source_from_publishing()
    {
        await using var fixture = await CreateDatabaseAsync();
        var firstSource = SourceId;
        var secondSource = Guid.Parse("d024ced1-3d4f-44aa-a5cb-32e9115259c3");
        fixture.DbContext.RateSources.Add(NewSource());
        fixture.DbContext.MarketRates.Add(new MarketRate
        {
            Id = Guid.NewGuid(),
            Pair = "USD/INR",
            Bid = 83.95m,
            Ask = 84.05m,
            Mid = 84m,
            SourceId = firstSource,
            FetchedAt = Now.AddMinutes(-2)
        });
        await fixture.DbContext.SaveChangesAsync();
        var job = CreateIngestionJob(
            fixture.DbContext,
            [
                new FailingRateProvider(firstSource),
                new StaticRateProvider(secondSource, [new RateQuote(
                    "USD/INR", 83.9m, 84.1m, 84m, secondSource, Now)])
                {
                    SourceName = "Independent source"
                }
            ]);

        await job.RunAsync(default);

        (await fixture.DbContext.MarketRates.CountAsync()).Should().Be(2);
        (await fixture.DbContext.MarketRates.SingleAsync(rate => rate.SourceId == firstSource))
            .IsStale.Should().BeTrue();
        (await fixture.DbContext.MarketRates.SingleAsync(rate => rate.SourceId == secondSource))
            .IsStale.Should().BeFalse();
    }

    [Theory]
    [InlineData(83.9, 84.1, true)]
    [InlineData(0, 84.1, false)]
    [InlineData(1000, 1001, false)]
    public async Task Sanity_guard_accepts_only_positive_rates_within_pair_bounds(
        decimal bid,
        decimal ask,
        bool accepted)
    {
        await using var fixture = await CreateDatabaseAsync();
        var provider = new StaticRateProvider(
            SourceId,
            [new RateQuote("USD/INR", bid, ask, (bid + ask) / 2m, SourceId, Now)]);
        var job = CreateIngestionJob(fixture.DbContext, [provider]);

        await job.RunAsync(default);

        (await fixture.DbContext.MarketRates.CountAsync()).Should().Be(accepted ? 1 : 0);
    }

    [Fact]
    public async Task Duplicate_quotes_are_suppressed_and_history_rollups_use_utc_bucket_boundaries()
    {
        await using var fixture = await CreateDatabaseAsync();
        var quote = Quote(84m, Now.AddMinutes(-1));
        var provider = new StaticRateProvider(SourceId, [quote]);
        var ingestionJob = CreateIngestionJob(fixture.DbContext, [provider]);
        await ingestionJob.RunAsync(default);
        await ingestionJob.RunAsync(default);
        (await fixture.DbContext.MarketRates.CountAsync()).Should().Be(1);

        fixture.DbContext.MarketRates.Add(new MarketRate
        {
            Id = Guid.NewGuid(),
            Pair = "USD/INR",
            Bid = 84.45m,
            Ask = 84.55m,
            Mid = 84.5m,
            SourceId = SourceId,
            FetchedAt = Now.AddSeconds(-20)
        });
        await fixture.DbContext.SaveChangesAsync();
        var rollup = new RateHistoryRollupJob(
            fixture.DbContext,
            Options.Create(new RateGuardOptions { HistoryRetentionDays = 400 }),
            new FixedTimeProvider(Now),
            NullLogger<RateHistoryRollupJob>.Instance);

        await rollup.RunAsync(default);
        await rollup.RunAsync(default);

        var history = await fixture.DbContext.RateHistories.ToDictionaryAsync(point => point.Bucket);
        history.Should().HaveCount(5);
        history[HistoryBucket.OneDay].Timestamp.Should().Be(
            new DateTimeOffset(2026, 10, 21, 11, 55, 0, TimeSpan.Zero));
        history[HistoryBucket.OneDay].Value.Should().Be(84.5m);
        history[HistoryBucket.OneWeek].Timestamp.Should().Be(
            new DateTimeOffset(2026, 10, 21, 11, 0, 0, TimeSpan.Zero));
        history[HistoryBucket.OneMonth].Timestamp.Should().Be(
            new DateTimeOffset(2026, 10, 21, 0, 0, 0, TimeSpan.Zero));
        history[HistoryBucket.SixMonths].Timestamp.Should().Be(
            new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero));
        history[HistoryBucket.OneYear].Timestamp.Should().Be(
            new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task Manual_override_requires_reason_and_expires_from_provider_results()
    {
        await using var fixture = await CreateDatabaseAsync();
        var options = Options.Create(new ManualOverrideOptions { SourceId = SourceId });
        var service = new ManualRateOverrideService(
            fixture.DbContext,
            options,
            new FixedTimeProvider(Now));
        await service.CreateAsync(
            new ManualRateOverrideRequest(
                "usd-inr",
                83.9m,
                84.1m,
                "Verified by operations",
                Now.AddHours(1),
                null),
            default);
        fixture.DbContext.ManualRateOverrides.Add(new ManualRateOverride
        {
            Id = Guid.NewGuid(),
            Pair = "OMR/INR",
            SourceId = SourceId,
            Bid = 218m,
            Ask = 219m,
            Mid = 218.5m,
            Reason = "Expired adjustment",
            CreatedAt = Now.AddDays(-1),
            ExpiresAt = Now.AddSeconds(-1)
        });
        await fixture.DbContext.SaveChangesAsync();
        var provider = new ManualRateOverrideProvider(
            fixture.DbContext,
            options,
            new FixedTimeProvider(Now));

        var quotes = await provider.FetchAsync(default);

        quotes.Should().ContainSingle().Which.Pair.Should().Be("USD/INR");
        (await fixture.DbContext.ManualRateOverrides.SingleAsync(rateOverride => rateOverride.Pair == "USD/INR"))
            .Reason.Should().Be("Verified by operations");
    }

    private static RateIngestionJob CreateIngestionJob(
        RemitoDbContext dbContext,
        IEnumerable<IRateProvider> providers,
        RecordingRateCache? cache = null) =>
        new(
            providers,
            dbContext,
            cache ?? new RecordingRateCache(),
            Options.Create(new RateGuardOptions
            {
                MaximumVariancePercent = 10m,
                HistoryRetentionDays = 400,
                PairBounds = new Dictionary<string, PairSanityBounds>
                {
                    ["USD/INR"] = new() { Minimum = 1, Maximum = 1000 }
                }
            }),
            new FixedTimeProvider(Now),
            new RatePipelineMetrics(new TelemetryClient(TelemetryConfiguration.CreateDefault())),
            NullLogger<RateIngestionJob>.Instance);

    private static RateQuote Quote(decimal mid, DateTimeOffset fetchedAt) =>
        new("USD/INR", mid - 0.05m, mid + 0.05m, mid, SourceId, fetchedAt);

    private static RateSource NewSource() => new()
    {
        Id = SourceId,
        Name = "Fixture source",
        Type = RateSourceType.Provider,
        Priority = 0,
        IsActive = true
    };

    private static async Task<DatabaseFixture> CreateDatabaseAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var context = new RemitoDbContext(
            new DbContextOptionsBuilder<RemitoDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        return new DatabaseFixture(connection, context);
    }

    private sealed class DatabaseFixture(SqliteConnection connection, RemitoDbContext dbContext) : IAsyncDisposable
    {
        public RemitoDbContext DbContext { get; } = dbContext;

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class StaticRateProvider(Guid sourceId, IReadOnlyList<RateQuote> quotes) : IRateProvider
    {
        public Guid SourceId { get; } = sourceId;
        public string SourceName { get; init; } = "Fixture source";
        public RateSourceType SourceType => RateSourceType.Provider;
        public TimeSpan StalenessThreshold => TimeSpan.FromMinutes(15);
        public Task<IReadOnlyList<RateQuote>> FetchAsync(CancellationToken cancellationToken) =>
            Task.FromResult(quotes);
    }

    private sealed class FailingRateProvider(Guid sourceId) : IRateProvider
    {
        public Guid SourceId { get; } = sourceId;
        public string SourceName => "Outage source";
        public RateSourceType SourceType => RateSourceType.Provider;
        public TimeSpan StalenessThreshold => TimeSpan.FromMinutes(15);
        public Task<IReadOnlyList<RateQuote>> FetchAsync(CancellationToken cancellationToken) =>
            throw new HttpRequestException("Fixture outage");
    }

    private sealed class RecordingRateCache : Remito.Application.Abstractions.IRateCache
    {
        public string? LastValue { get; private set; }

        public ValueTask<string?> GetAsync(string key, CancellationToken cancellationToken) =>
            ValueTask.FromResult(LastValue);

        public ValueTask SetAsync(
            string key,
            string value,
            TimeSpan lifetime,
            CancellationToken cancellationToken)
        {
            LastValue = value;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingHandler(string body) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class FixedHttpClientFactory(HttpClient httpClient) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => httpClient;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
