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
        builder.Property(favourite => favourite.TargetCode).HasMaxLength(10);
        builder.HasIndex(favourite => new
        {
            favourite.UserId,
            favourite.Type,
            favourite.TargetId,
            favourite.TargetCode
        }).IsUnique().HasFilter(null);
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
        builder.HasIndex(conversion => new { conversion.ExchangeId, conversion.ExternalRef }).IsUnique()
            .HasFilter("[ExternalRef] IS NOT NULL");
        builder.HasOne<Exchange>().WithMany().HasForeignKey(conversion => conversion.ExchangeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(conversion => conversion.RedirectEvent).WithMany()
            .HasForeignKey(conversion => conversion.ClickId).HasPrincipalKey(redirect => redirect.ClickId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    internal sealed class PartnerReconciliationIssueConfiguration : IEntityTypeConfiguration<PartnerReconciliationIssue>
    {
        public void Configure(EntityTypeBuilder<PartnerReconciliationIssue> builder)
        {
            builder.HasKey(issue => issue.Id);
            builder.Property(issue => issue.ExternalRef).HasMaxLength(200).IsRequired();
            builder.Property(issue => issue.ClickId).HasMaxLength(100);
            builder.Property(issue => issue.IssueType).HasMaxLength(50).IsRequired();
            builder.HasIndex(issue => new { issue.ExchangeId, issue.ExternalRef }).IsUnique();
            builder.HasOne<Exchange>().WithMany().HasForeignKey(issue => issue.ExchangeId)
                .OnDelete(DeleteBehavior.Restrict);
        }

        internal sealed class PartnerReportRowConfiguration : IEntityTypeConfiguration<PartnerReportRow>
        {
            public void Configure(EntityTypeBuilder<PartnerReportRow> builder)
            {
                builder.HasKey(row => row.Id);
                builder.Property(row => row.ExternalRef).HasMaxLength(200).IsRequired();
                builder.Property(row => row.ClickId).HasMaxLength(100).IsRequired();
                builder.Property(row => row.Amount).HasPrecision(18, 2);
                builder.Property(row => row.Commission).HasPrecision(18, 2);
                builder.Property(row => row.CurrencyCode).HasMaxLength(3).IsFixedLength().IsRequired();
                builder.HasIndex(row => new { row.ExchangeId, row.ExternalRef }).IsUnique();
                builder.HasIndex(row => new { row.ExchangeId, row.ConvertedAt });
                builder.HasOne<Exchange>().WithMany().HasForeignKey(row => row.ExchangeId)
                    .OnDelete(DeleteBehavior.Restrict);
                builder.HasOne<Currency>().WithMany().HasForeignKey(row => row.CurrencyCode)
                    .OnDelete(DeleteBehavior.Restrict);
            }
        }
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
