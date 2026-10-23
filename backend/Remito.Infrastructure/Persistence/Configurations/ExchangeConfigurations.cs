using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Remito.Domain.Entities;

namespace Remito.Infrastructure.Persistence.Configurations;

internal sealed class ExchangeConfiguration : IEntityTypeConfiguration<Exchange>
{
    private static readonly Exchange[] Placeholders =
    new[]
    {
        "Placeholder Exchange 01", "Placeholder Exchange 02", "Placeholder Exchange 03",
        "Placeholder Exchange 04", "Placeholder Exchange 05", "Placeholder Exchange 06",
        "Placeholder Exchange 07", "Placeholder Exchange 08", "Placeholder Exchange 09",
        "Placeholder Exchange 10"
    }.Select((name, index) => new Exchange
    {
        Id = Guid.Parse($"10000000-0000-0000-0000-{index + 1:000000000000}"),
        Name = name,
        Slug = $"placeholder-exchange-{index + 1:00}",
        Status = ExchangeStatus.Pending,
        Tier = ExchangeTier.Standard,
        IsPlaceholder = true,
        HasApp = false
    }).ToArray();

    public void Configure(EntityTypeBuilder<Exchange> builder)
    {
        builder.HasKey(exchange => exchange.Id);
        builder.Property(exchange => exchange.Name).HasMaxLength(200).IsRequired();
        builder.Property(exchange => exchange.Slug).HasMaxLength(200).IsRequired();
        builder.Property(exchange => exchange.LogoUrl).HasMaxLength(2048);
        builder.Property(exchange => exchange.AppDeepLink).HasMaxLength(2048);
        builder.Property(exchange => exchange.PlayStoreUrl).HasMaxLength(2048);
        builder.Property(exchange => exchange.GatewayUrl).HasMaxLength(2048);
        builder.Property(exchange => exchange.AllowedRedirectHosts).HasMaxLength(2048);
        builder.Property(exchange => exchange.CommissionFlatAmount).HasPrecision(18, 2);
        builder.Property(exchange => exchange.CommissionFlatAmount).HasDefaultValue(0.50m);
        builder.Property(exchange => exchange.CommissionCurrencyCode).HasMaxLength(3).IsFixedLength();
        builder.Property(exchange => exchange.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(exchange => exchange.Tier).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(exchange => exchange.Slug).IsUnique();
        builder.HasIndex(exchange => new { exchange.Status, exchange.Tier });
        builder.HasOne<Currency>().WithMany().HasForeignKey(exchange => exchange.CommissionCurrencyCode)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasData(Placeholders);
    }
}

internal sealed class ExchangeRateConfiguration : IEntityTypeConfiguration<ExchangeRate>
{
    public void Configure(EntityTypeBuilder<ExchangeRate> builder)
    {
        builder.HasKey(rate => rate.Id);
        builder.Property(rate => rate.Rate).HasPrecision(18, 6);
        builder.HasIndex(rate => new { rate.CorridorId, rate.IsStale, rate.Rate });
        builder.HasIndex(rate => new { rate.ExchangeId, rate.CorridorId, rate.FetchedAt })
            .IsDescending(false, false, true);
        builder.HasOne(rate => rate.Exchange).WithMany().HasForeignKey(rate => rate.ExchangeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(rate => rate.Corridor).WithMany().HasForeignKey(rate => rate.CorridorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ExchangeFeeConfiguration : IEntityTypeConfiguration<ExchangeFee>
{
    public void Configure(EntityTypeBuilder<ExchangeFee> builder)
    {
        builder.HasKey(fee => fee.Id);
        builder.Property(fee => fee.MinAmount).HasPrecision(18, 2);
        builder.Property(fee => fee.MaxAmount).HasPrecision(18, 2);
        builder.Property(fee => fee.FlatFee).HasPrecision(18, 2);
        builder.Property(fee => fee.PercentFee).HasPrecision(9, 6);
        builder.HasIndex(fee => new { fee.ExchangeId, fee.CorridorId, fee.MinAmount, fee.MaxAmount });
        builder.HasOne(fee => fee.Exchange).WithMany().HasForeignKey(fee => fee.ExchangeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(fee => fee.Corridor).WithMany().HasForeignKey(fee => fee.CorridorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
