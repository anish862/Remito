using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Remito.Domain.Entities;
using Remito.Infrastructure.Persistence;

namespace Remito.UnitTests;

public sealed class DatabaseSchemaTests
{
    private static RemitoDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<RemitoDbContext>()
            .UseSqlServer("Server=localhost;Database=RemitoSchemaTests;Trusted_Connection=True;TrustServerCertificate=True")
            .Options);

    [Fact]
    public void Rate_and_amount_columns_use_required_decimal_precision()
    {
        using var dbContext = CreateDbContext();

        AssertPrecision<MarketRate>(dbContext, nameof(MarketRate.Mid), 18, 6);
        AssertPrecision<MarketRate>(dbContext, nameof(MarketRate.Bid), 18, 6);
        AssertPrecision<MarketRate>(dbContext, nameof(MarketRate.Ask), 18, 6);
        AssertPrecision<RateHistory>(dbContext, nameof(RateHistory.Value), 18, 6);
        AssertPrecision<ExchangeRate>(dbContext, nameof(ExchangeRate.Rate), 18, 6);
        AssertPrecision<ExchangeFee>(dbContext, nameof(ExchangeFee.MinAmount), 18, 2);
        AssertPrecision<ExchangeFee>(dbContext, nameof(ExchangeFee.MaxAmount), 18, 2);
        AssertPrecision<ExchangeFee>(dbContext, nameof(ExchangeFee.FlatFee), 18, 2);
        AssertPrecision<ExchangeFee>(dbContext, nameof(ExchangeFee.PercentFee), 9, 6);
        AssertPrecision<RedirectEvent>(dbContext, nameof(RedirectEvent.Amount), 18, 2);
        AssertPrecision<ReferralConversion>(dbContext, nameof(ReferralConversion.Amount), 18, 2);
        AssertPrecision<CommissionLedger>(dbContext, nameof(CommissionLedger.Amount), 18, 2);
    }

    [Fact]
    public void History_bucket_persists_the_contract_values()
    {
        using var dbContext = CreateDbContext();
        var property = dbContext.Model.FindEntityType(typeof(RateHistory))!
            .FindProperty(nameof(RateHistory.Bucket))!;
        var converter = property.GetValueConverter()!;

        Assert.Equal("1D", converter.ConvertToProvider(HistoryBucket.OneDay));
        Assert.Equal("1W", converter.ConvertToProvider(HistoryBucket.OneWeek));
        Assert.Equal("1M", converter.ConvertToProvider(HistoryBucket.OneMonth));
        Assert.Equal("6M", converter.ConvertToProvider(HistoryBucket.SixMonths));
        Assert.Equal("1Y", converter.ConvertToProvider(HistoryBucket.OneYear));
    }

    [Fact]
    public void Required_indexes_and_seed_data_are_present()
    {
        using var dbContext = CreateDbContext();
        var model = dbContext.GetService<IDesignTimeModel>().Model;
        var marketRateIndexes = model.FindEntityType(typeof(MarketRate))!.GetIndexes();
        var redirectIndexes = model.FindEntityType(typeof(RedirectEvent))!.GetIndexes();
        var historyIndexes = model.FindEntityType(typeof(RateHistory))!.GetIndexes();
        var exchangeRateIndexes = model.FindEntityType(typeof(ExchangeRate))!.GetIndexes();
        var corridorIndexes = model.FindEntityType(typeof(Corridor))!.GetIndexes();
        var currencies = model.FindEntityType(typeof(Currency))!.GetSeedData();
        var corridors = model.FindEntityType(typeof(Corridor))!.GetSeedData();
        var exchanges = model.FindEntityType(typeof(Exchange))!.GetSeedData();

        Assert.Contains(redirectIndexes, index =>
            index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual([nameof(RedirectEvent.ClickId)]));
        Assert.Contains(marketRateIndexes, index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(MarketRate.Pair), nameof(MarketRate.FetchedAt)]));
        Assert.Contains(historyIndexes, index =>
            index.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(RateHistory.Pair), nameof(RateHistory.Bucket), nameof(RateHistory.Timestamp)]));
        Assert.Contains(exchangeRateIndexes, index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(ExchangeRate.CorridorId), nameof(ExchangeRate.IsStale), nameof(ExchangeRate.Rate)]));
        Assert.Contains(corridorIndexes, index =>
            index.IsUnique && index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(Corridor.FromCurrency), nameof(Corridor.ToCurrency)]));
        Assert.Equal(10, currencies.Count());
        Assert.Contains(corridors, corridor =>
            corridor[nameof(Corridor.FromCurrency)] as string == "OMR"
            && corridor[nameof(Corridor.ToCurrency)] as string == "INR");
        Assert.Equal(10, exchanges.Count());
        Assert.All(exchanges, exchange => Assert.Equal(true, exchange[nameof(Exchange.IsPlaceholder)]));
    }

    private static void AssertPrecision<TEntity>(
        RemitoDbContext dbContext,
        string propertyName,
        int precision,
        int scale)
    {
        var property = dbContext.Model.FindEntityType(typeof(TEntity))!.FindProperty(propertyName)!;
        Assert.Equal(precision, property.GetPrecision());
        Assert.Equal(scale, property.GetScale());
    }
}
