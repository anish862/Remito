namespace Remito.Domain.Entities;

public sealed class Exchange
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? LogoUrl { get; set; }
    public bool HasApp { get; set; }
    public string? AppDeepLink { get; set; }
    public string? PlayStoreUrl { get; set; }
    public string? GatewayUrl { get; set; }
    public ExchangeStatus Status { get; set; }
    public ExchangeTier Tier { get; set; }
    public bool IsPlaceholder { get; set; }
}

public enum ExchangeStatus
{
    Pending,
    Active,
    Suspended,
    Inactive
}

public enum ExchangeTier
{
    Standard,
    Featured,
    Sponsored
}

public sealed class ExchangeRate
{
    public Guid Id { get; set; }
    public Guid ExchangeId { get; set; }
    public Guid CorridorId { get; set; }
    public decimal Rate { get; set; }
    public DateTimeOffset FetchedAt { get; set; }
    public bool IsStale { get; set; }
    public Exchange Exchange { get; set; } = null!;
    public Corridor Corridor { get; set; } = null!;
}

public sealed class ExchangeFee
{
    public Guid Id { get; set; }
    public Guid ExchangeId { get; set; }
    public Guid CorridorId { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public decimal? FlatFee { get; set; }
    public decimal? PercentFee { get; set; }
    public Exchange Exchange { get; set; } = null!;
    public Corridor Corridor { get; set; } = null!;
}
