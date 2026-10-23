namespace Remito.Domain.Entities;

public sealed class RateSource
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public RateSourceType Type { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; }
}

public enum RateSourceType
{
    Provider,
    Exchange,
    Manual
}

public sealed class MarketRate
{
    public Guid Id { get; set; }
    public required string Pair { get; set; }
    public decimal Bid { get; set; }
    public decimal Ask { get; set; }
    public decimal Mid { get; set; }
    public Guid SourceId { get; set; }
    public DateTimeOffset FetchedAt { get; set; }
    public bool IsStale { get; set; }
    public RateSource Source { get; set; } = null!;
}

public sealed class RateHistory
{
    public Guid Id { get; set; }
    public required string Pair { get; set; }
    public HistoryBucket Bucket { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public decimal Value { get; set; }
}

public sealed class RateQuarantine
{
    public Guid Id { get; set; }
    public required string Pair { get; set; }
    public Guid SourceId { get; set; }
    public decimal Bid { get; set; }
    public decimal Ask { get; set; }
    public decimal Mid { get; set; }
    public DateTimeOffset FetchedAt { get; set; }
    public required string Reason { get; set; }
    public DateTimeOffset QuarantinedAt { get; set; }
}

public sealed class ManualRateOverride
{
    public Guid Id { get; set; }
    public required string Pair { get; set; }
    public Guid SourceId { get; set; }
    public decimal Bid { get; set; }
    public decimal Ask { get; set; }
    public decimal Mid { get; set; }
    public required string Reason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

public enum HistoryBucket
{
    OneDay,
    OneWeek,
    OneMonth,
    SixMonths,
    OneYear
}

public sealed class Alert
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string Pair { get; set; }
    public AlertDirection Direction { get; set; }
    public decimal Target { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset? LastTriggeredAt { get; set; }
    public bool IsOneTime { get; set; }
    public bool DailySummary { get; set; }
    public bool IsArmed { get; set; } = true;
    public User User { get; set; } = null!;
}

public enum AlertDirection
{
    Above,
    Below
}
