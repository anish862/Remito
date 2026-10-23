using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Remito.Application.Abstractions;
using Remito.Application.Exchanges;
using Remito.Domain.Entities;
using Remito.Infrastructure.Persistence;
using Remito.Infrastructure.Rates;

namespace Remito.UnitTests;

public sealed class ExchangeComparisonServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 4, 0, 0, TimeSpan.Zero);
    private static readonly Guid CorridorId = Guid.Parse("0f43b2c9-44b3-4b91-8e7f-2aa9e19ecb01");

    [Fact]
    public async Task Compare_ranks_known_amount_received_then_unknown_fees_by_rate_and_includes_disclosure()
    {
        await using var fixture = await CreateFixtureAsync();
        fixture.AddExchange("Best net", 220m, flatFee: 1m, percentFee: 1.5m);
        fixture.AddExchange("Best raw", 225m);

        var response = await fixture.CompareAsync(amount: 100m);

        Assert.Equal("Best net", response.Exchanges[0].Name);
        Assert.Equal(2.5m, response.Exchanges[0].Fee);
        Assert.Equal(21_450m, response.Exchanges[0].AmountReceived);
        var noFeeOffer = Assert.Single(response.Exchanges, offer => offer.Name == "Best raw");
        Assert.Null(noFeeOffer.Fee);
        Assert.Null(noFeeOffer.AmountReceived);
        Assert.Equal("Fee not provided", noFeeOffer.FeeDisclosure);
        Assert.Contains("does not process payments", response.Disclaimer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Compare_marks_rate_ties_stale_quotes_and_sponsored_slot_without_sponsorship_boost()
    {
        await using var fixture = await CreateFixtureAsync();
        var firstTie = fixture.AddExchange("Tie A", 220m, flatFee: 1m);
        var secondTie = fixture.AddExchange("Tie B", 220m, flatFee: 1m);
        fixture.AddExchange("Sponsored lower quote", 210m, tier: ExchangeTier.Sponsored, stale: true);

        var response = await fixture.CompareAsync(amount: 10m);

        Assert.All(response.Exchanges.Where(item => item.Name is "Tie A" or "Tie B"),
            item => Assert.True(item.IsBestRate));
        Assert.False(response.SponsoredSlot!.Exchanges.Single().IsBestRate);
        Assert.True(response.SponsoredSlot.Exchanges.Single().IsStale);
        Assert.Equal("Sponsored", response.SponsoredSlot.Label);
        Assert.Equal(new[] { firstTie.Id, secondTie.Id }, response.Exchanges.Select(item => item.Id));
    }

    [Fact]
    public async Task Compare_excludes_non_active_exchange_quotes_from_results_and_best_rate()
    {
        await using var fixture = await CreateFixtureAsync();
        fixture.AddExchange("Active exchange", 220m, flatFee: 1m);
        fixture.AddExchange("Pending exchange", 999m, status: ExchangeStatus.Pending);
        fixture.AddExchange("Suspended exchange", 1_000m, status: ExchangeStatus.Suspended);

        var response = await fixture.CompareAsync();

        var offer = Assert.Single(response.Exchanges);
        Assert.Equal("Active exchange", offer.Name);
        Assert.True(offer.IsBestRate);
    }

    [Fact]
    public async Task Compare_selects_fee_band_and_rounds_fee_and_received_amount()
    {
        await using var fixture = await CreateFixtureAsync();
        var exchange = fixture.AddExchange("Band fees", 200.123m);
        fixture.AddFee(exchange.Id, minAmount: 50m, maxAmount: 100m, flatFee: 1.005m, percentFee: 0m);
        fixture.AddFee(exchange.Id, minAmount: 100.01m, maxAmount: null, flatFee: 2m, percentFee: 0m);

        var response = await fixture.CompareAsync(amount: 100m);

        var offer = Assert.Single(response.Exchanges);
        Assert.Equal(1.005m, offer.Fee);
        Assert.Equal(19_811.18m, offer.AmountReceived);
    }

    [Fact]
    public async Task Compare_filters_by_app_and_search_without_changing_best_rate()
    {
        await using var fixture = await CreateFixtureAsync();
        fixture.AddExchange("App Exchange", 230m, flatFee: 1m, hasApp: true);
        fixture.AddExchange("Web Exchange", 220m, flatFee: 1m, hasApp: false);

        var response = await fixture.CompareAsync(hasApp: false, search: "web");

        var offer = Assert.Single(response.Exchanges);
        Assert.Equal("Web Exchange", offer.Name);
        Assert.False(offer.IsBestRate);
        Assert.Equal(1, response.TotalCount);
    }

    [Fact]
    public async Task Compare_ordering_is_stable_and_amount_cache_is_scoped_for_sixty_seconds()
    {
        await using var fixture = await CreateFixtureAsync();
        fixture.AddExchange("Alpha", 220m, flatFee: 1m);
        fixture.AddExchange("Beta", 220m, flatFee: 1m);
        fixture.AddExchange("Gamma", 220m, flatFee: 1m);

        var first = await fixture.CompareAsync(amount: 10m);
        var second = await fixture.CompareAsync(amount: 10m, cache: false);
        var otherAmount = await fixture.CompareAsync(amount: 11m);

        Assert.Equal(first.Exchanges.Select(item => item.Id), second.Exchanges.Select(item => item.Id));
        Assert.Equal(new[] { "Alpha", "Beta", "Gamma" }, first.Exchanges.Select(item => item.Name));
        Assert.Equal(10m, first.Amount);
        Assert.Equal(11m, otherAmount.Amount);
        Assert.Contains(fixture.Cache.Lifetimes, lifetime => lifetime == TimeSpan.FromSeconds(60));
        Assert.Contains(fixture.Cache.Keys, key => key.EndsWith(":10", StringComparison.Ordinal));
        Assert.Contains(fixture.Cache.Keys, key => key.EndsWith(":11", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Compare_rejects_non_positive_amount(decimal amount)
    {
        await using var fixture = await CreateFixtureAsync();

        var exception = await Assert.ThrowsAsync<ExchangeComparisonException>(
            () => fixture.CompareAsync(amount));

        Assert.Equal(400, exception.StatusCode);
    }

    private static async Task<Fixture> CreateFixtureAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var context = new RemitoDbContext(
            new DbContextOptionsBuilder<RemitoDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        return new Fixture(connection, context);
    }

    private sealed class Fixture(SqliteConnection connection, RemitoDbContext context) : IAsyncDisposable
    {
        public RemitoDbContext Context { get; } = context;
        public RecordingRateCache Cache { get; } = new();

        public Exchange AddExchange(
            string name,
            decimal rate,
            decimal? flatFee = null,
            decimal? percentFee = null,
            ExchangeTier tier = ExchangeTier.Standard,
            bool stale = false,
            bool hasApp = true,
            ExchangeStatus status = ExchangeStatus.Active)
        {
            var exchange = new Exchange
            {
                Id = Guid.NewGuid(),
                Name = name,
                Slug = name.ToLowerInvariant().Replace(' ', '-'),
                Status = status,
                Tier = tier,
                HasApp = hasApp,
                IsPlaceholder = false
            };
            Context.Exchanges.Add(exchange);
            Context.ExchangeRates.Add(new ExchangeRate
            {
                Id = Guid.NewGuid(),
                ExchangeId = exchange.Id,
                CorridorId = CorridorId,
                Rate = rate,
                FetchedAt = Now,
                IsStale = stale
            });
            if (flatFee is not null || percentFee is not null)
            {
                AddFee(exchange.Id, null, null, flatFee, percentFee);
            }

            Context.SaveChanges();
            return exchange;
        }

        public void AddFee(
            Guid exchangeId,
            decimal? minAmount,
            decimal? maxAmount,
            decimal? flatFee,
            decimal? percentFee)
        {
            Context.ExchangeFees.Add(new ExchangeFee
            {
                Id = Guid.NewGuid(),
                ExchangeId = exchangeId,
                CorridorId = CorridorId,
                MinAmount = minAmount,
                MaxAmount = maxAmount,
                FlatFee = flatFee,
                PercentFee = percentFee
            });
            Context.SaveChanges();
        }

        public Task<ExchangeComparisonResponse> CompareAsync(
            decimal amount = 10m,
            bool? hasApp = null,
            string? search = null,
            bool cache = true) =>
            new ExchangeComparisonService(
                    Context,
                    cache ? Cache : new RecordingRateCache(),
                    NullLogger<ExchangeComparisonService>.Instance)
                .GetExchangesAsync(
                    "OMR",
                    "INR",
                    amount,
                    new ExchangeComparisonOptions(null, hasApp, search, 1, 100),
                    default);

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class RecordingRateCache : IRateCache
    {
        private readonly Dictionary<string, string> values = new(StringComparer.Ordinal);
        public List<string> Keys { get; } = [];
        public List<TimeSpan> Lifetimes { get; } = [];

        public ValueTask<string?> GetAsync(string key, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Keys.Add(key);
            values.TryGetValue(key, out var value);
            return ValueTask.FromResult(value);
        }

        public ValueTask SetAsync(
            string key,
            string value,
            TimeSpan lifetime,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Keys.Add(key);
            Lifetimes.Add(lifetime);
            values[key] = value;
            return ValueTask.CompletedTask;
        }
    }
}
