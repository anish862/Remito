using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Remito.Domain.Entities;

namespace Remito.Infrastructure.Persistence.Configurations;

internal sealed class FavouriteConfiguration : IEntityTypeConfiguration<Favourite>
{
    public void Configure(EntityTypeBuilder<Favourite> builder)
    {
        builder.HasKey(favourite => favourite.Id);
        builder.Property(favourite => favourite.Type).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(favourite => new { favourite.UserId, favourite.Type, favourite.TargetId }).IsUnique();
        builder.HasOne(favourite => favourite.User).WithMany().HasForeignKey(favourite => favourite.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RedirectEventConfiguration : IEntityTypeConfiguration<RedirectEvent>
{
    public void Configure(EntityTypeBuilder<RedirectEvent> builder)
    {
        builder.HasKey(redirect => redirect.Id);
        builder.Property(redirect => redirect.ClickId).HasMaxLength(100).IsRequired();
        builder.Property(redirect => redirect.Amount).HasPrecision(18, 2);
        builder.Property(redirect => redirect.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(redirect => redirect.ClickId).IsUnique();
        builder.HasIndex(redirect => new { redirect.CorridorId, redirect.CreatedAt });
        builder.HasIndex(redirect => new { redirect.ExchangeId, redirect.CreatedAt });
        builder.HasOne(redirect => redirect.User).WithMany().HasForeignKey(redirect => redirect.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(redirect => redirect.Exchange).WithMany().HasForeignKey(redirect => redirect.ExchangeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(redirect => redirect.Corridor).WithMany().HasForeignKey(redirect => redirect.CorridorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ReferralConversionConfiguration : IEntityTypeConfiguration<ReferralConversion>
{
    public void Configure(EntityTypeBuilder<ReferralConversion> builder)
    {
        builder.HasKey(conversion => conversion.Id);
        builder.Property(conversion => conversion.ClickId).HasMaxLength(100).IsRequired();
        builder.Property(conversion => conversion.ExternalRef).HasMaxLength(200);
        builder.Property(conversion => conversion.Amount).HasPrecision(18, 2);
        builder.Property(conversion => conversion.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(conversion => conversion.ClickId).IsUnique();
        builder.HasIndex(conversion => conversion.ExternalRef).IsUnique()
            .HasFilter("[ExternalRef] IS NOT NULL");
        builder.HasOne(conversion => conversion.RedirectEvent).WithMany()
            .HasForeignKey(conversion => conversion.ClickId).HasPrincipalKey(redirect => redirect.ClickId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CommissionLedgerConfiguration : IEntityTypeConfiguration<CommissionLedger>
{
    public void Configure(EntityTypeBuilder<CommissionLedger> builder)
    {
        builder.HasKey(ledger => ledger.Id);
        builder.Property(ledger => ledger.Amount).HasPrecision(18, 2);
        builder.Property(ledger => ledger.CurrencyCode).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(ledger => ledger.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(ledger => ledger.ConversionId).IsUnique();
        builder.HasIndex(ledger => new { ledger.Status, ledger.CurrencyCode });
        builder.HasOne(ledger => ledger.Conversion).WithMany()
            .HasForeignKey(ledger => ledger.ConversionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(ledger => ledger.Currency).WithMany()
            .HasForeignKey(ledger => ledger.CurrencyCode).OnDelete(DeleteBehavior.Restrict);
    }
}
