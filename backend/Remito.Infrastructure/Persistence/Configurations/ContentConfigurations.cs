using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Remito.Domain.Entities;

namespace Remito.Infrastructure.Persistence.Configurations;

internal sealed class AdCampaignConfiguration : IEntityTypeConfiguration<AdCampaign>
{
    public void Configure(EntityTypeBuilder<AdCampaign> builder)
    {
        builder.HasKey(campaign => campaign.Id);
        builder.Property(campaign => campaign.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(campaign => new { campaign.IsActive, campaign.StartsAt, campaign.EndsAt });
    }
}

internal sealed class AdCreativeConfiguration : IEntityTypeConfiguration<AdCreative>
{
    public void Configure(EntityTypeBuilder<AdCreative> builder)
    {
        builder.HasKey(creative => creative.Id);
        builder.Property(creative => creative.Slot).HasMaxLength(100).IsRequired();
        builder.Property(creative => creative.ImageUrl).HasMaxLength(2048).IsRequired();
        builder.Property(creative => creative.TargetUrl).HasMaxLength(2048).IsRequired();
        builder.Property(creative => creative.AltText).HasMaxLength(500).IsRequired();
        builder.HasIndex(creative => new { creative.Slot, creative.SortOrder });
        builder.HasOne(creative => creative.Campaign).WithMany().HasForeignKey(creative => creative.CampaignId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AdImpressionConfiguration : IEntityTypeConfiguration<AdImpression>
{
    public void Configure(EntityTypeBuilder<AdImpression> builder)
    {
        builder.HasKey(impression => impression.Id);
        builder.HasIndex(impression => new { impression.CreativeId, impression.CreatedAt });
        builder.HasOne(impression => impression.Creative).WithMany().HasForeignKey(impression => impression.CreativeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(impression => impression.User).WithMany().HasForeignKey(impression => impression.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class NewsItemConfiguration : IEntityTypeConfiguration<NewsItem>
{
    public void Configure(EntityTypeBuilder<NewsItem> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Title).HasMaxLength(300).IsRequired();
        builder.Property(item => item.Slug).HasMaxLength(300).IsRequired();
        builder.Property(item => item.Summary).HasMaxLength(1000).IsRequired();
        builder.Property(item => item.Body).IsRequired();
        builder.Property(item => item.SourceUrl).HasMaxLength(2048);
        builder.HasIndex(item => item.Slug).IsUnique();
        builder.HasIndex(item => new { item.IsPublished, item.PublishedAt })
            .IsDescending(false, true);
    }
}

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(log => log.Id);
        builder.Property(log => log.Action).HasMaxLength(100).IsRequired();
        builder.Property(log => log.EntityType).HasMaxLength(200).IsRequired();
        builder.Property(log => log.EntityId).HasMaxLength(200);
        builder.Property(log => log.Details).HasColumnType("nvarchar(max)");
        builder.HasIndex(log => new { log.EntityType, log.EntityId, log.CreatedAt });
        builder.HasIndex(log => new { log.UserId, log.CreatedAt });
        builder.HasOne(log => log.User).WithMany().HasForeignKey(log => log.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
