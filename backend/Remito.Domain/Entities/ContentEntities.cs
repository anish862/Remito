namespace Remito.Domain.Entities;

public sealed class AdCampaign
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public bool IsActive { get; set; }
}

public sealed class AdCreative
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public required string Slot { get; set; }
    public required string ImageUrl { get; set; }
    public required string TargetUrl { get; set; }
    public required string AltText { get; set; }
    public int SortOrder { get; set; }
    public AdCampaign Campaign { get; set; } = null!;
}

public sealed class AdImpression
{
    public Guid Id { get; set; }
    public Guid CreativeId { get; set; }
    public Guid? UserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public AdCreative Creative { get; set; } = null!;
    public User? User { get; set; }
}

public sealed class NewsItem
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public required string Slug { get; set; }
    public required string Summary { get; set; }
    public required string Body { get; set; }
    public string? SourceUrl { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public bool IsPublished { get; set; }
}

public sealed class AuditLog
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public required string Action { get; set; }
    public required string EntityType { get; set; }
    public string? EntityId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? Details { get; set; }
    public User? User { get; set; }
}
