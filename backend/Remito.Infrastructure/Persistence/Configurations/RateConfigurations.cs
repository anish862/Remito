using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Remito.Domain.Entities;

namespace Remito.Infrastructure.Persistence.Configurations;

internal sealed class RateSourceConfiguration : IEntityTypeConfiguration<RateSource>
{
    public void Configure(EntityTypeBuilder<RateSource> builder)
    {
        builder.HasKey(source => source.Id);
        builder.Property(source => source.Name).HasMaxLength(200).IsRequired();
        builder.Property(source => source.Type).HasConversion<string>().HasMaxLength(30);
        builder.HasIndex(source => new { source.IsActive, source.Priority });
        builder.HasIndex(source => source.Name).IsUnique();
    }
}

internal sealed class MarketRateConfiguration : IEntityTypeConfiguration<MarketRate>
{
    public void Configure(EntityTypeBuilder<MarketRate> builder)
    {
        builder.HasKey(rate => rate.Id);
        builder.Property(rate => rate.Pair).HasMaxLength(7).IsRequired();
        builder.Property(rate => rate.Bid).HasPrecision(18, 6);
        builder.Property(rate => rate.Ask).HasPrecision(18, 6);
        builder.Property(rate => rate.Mid).HasPrecision(18, 6);
        builder.HasIndex(rate => new { rate.Pair, rate.FetchedAt }).IsDescending(false, true)
            .IncludeProperties(rate => new { rate.Mid, rate.SourceId, rate.IsStale });
        builder.HasIndex(rate => new { rate.SourceId, rate.Pair, rate.FetchedAt });
        builder.HasOne(rate => rate.Source).WithMany().HasForeignKey(rate => rate.SourceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RateHistoryConfiguration : IEntityTypeConfiguration<RateHistory>
{
    private static readonly ValueConverter<HistoryBucket, string> BucketConverter =
        new(bucket => BucketToString(bucket), value => StringToBucket(value));

    public void Configure(EntityTypeBuilder<RateHistory> builder)
    {
        builder.HasKey(history => history.Id);
        builder.Property(history => history.Pair).HasMaxLength(7).IsRequired();
        builder.Property(history => history.Bucket).HasConversion(BucketConverter).HasMaxLength(2);
        builder.Property(history => history.Value).HasPrecision(18, 6);
        builder.HasIndex(history => new { history.Pair, history.Bucket, history.Timestamp })
            .IsDescending(false, false, true).IsUnique();
    }

    private static string BucketToString(HistoryBucket bucket) =>
        bucket switch
        {
            HistoryBucket.OneDay => "1D",
            HistoryBucket.OneWeek => "1W",
            HistoryBucket.OneMonth => "1M",
            HistoryBucket.SixMonths => "6M",
            HistoryBucket.OneYear => "1Y",
            _ => throw new ArgumentOutOfRangeException(nameof(bucket), bucket, null)
        };

    private static HistoryBucket StringToBucket(string value) =>
        value switch
        {
            "1D" => HistoryBucket.OneDay,
            "1W" => HistoryBucket.OneWeek,
            "1M" => HistoryBucket.OneMonth,
            "6M" => HistoryBucket.SixMonths,
            "1Y" => HistoryBucket.OneYear,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
}

internal sealed class RateQuarantineConfiguration : IEntityTypeConfiguration<RateQuarantine>
{
    public void Configure(EntityTypeBuilder<RateQuarantine> builder)
    {
        builder.HasKey(quarantine => quarantine.Id);
        builder.Property(quarantine => quarantine.Pair).HasMaxLength(7).IsRequired();
        builder.Property(quarantine => quarantine.Bid).HasPrecision(18, 6);
        builder.Property(quarantine => quarantine.Ask).HasPrecision(18, 6);
        builder.Property(quarantine => quarantine.Mid).HasPrecision(18, 6);
        builder.Property(quarantine => quarantine.Reason).HasMaxLength(500).IsRequired();
        builder.HasIndex(quarantine => new { quarantine.SourceId, quarantine.Pair, quarantine.QuarantinedAt });
        builder.HasOne<RateSource>().WithMany().HasForeignKey(quarantine => quarantine.SourceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ManualRateOverrideConfiguration : IEntityTypeConfiguration<ManualRateOverride>
{
    public void Configure(EntityTypeBuilder<ManualRateOverride> builder)
    {
        builder.HasKey(rateOverride => rateOverride.Id);
        builder.Property(rateOverride => rateOverride.Pair).HasMaxLength(7).IsRequired();
        builder.Property(rateOverride => rateOverride.Bid).HasPrecision(18, 6);
        builder.Property(rateOverride => rateOverride.Ask).HasPrecision(18, 6);
        builder.Property(rateOverride => rateOverride.Mid).HasPrecision(18, 6);
        builder.Property(rateOverride => rateOverride.Reason).HasMaxLength(500).IsRequired();
        builder.HasIndex(rateOverride => new { rateOverride.Pair, rateOverride.ExpiresAt });
        builder.HasOne<RateSource>().WithMany().HasForeignKey(rateOverride => rateOverride.SourceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> builder)
    {
        builder.HasKey(alert => alert.Id);
        builder.Property(alert => alert.Pair).HasMaxLength(7).IsRequired();
        builder.Property(alert => alert.Direction).HasConversion<string>().HasMaxLength(10);
        builder.Property(alert => alert.Target).HasPrecision(18, 6);
        builder.HasIndex(alert => new { alert.UserId, alert.IsActive });
        builder.HasIndex(alert => new { alert.Pair, alert.IsActive, alert.Direction, alert.Target });
        builder.Property(alert => alert.IsOneTime).HasDefaultValue(false);
        builder.Property(alert => alert.DailySummary).HasDefaultValue(false);
        builder.Property(alert => alert.IsArmed).HasDefaultValue(true);
        builder.HasOne(alert => alert.User).WithMany().HasForeignKey(alert => alert.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
