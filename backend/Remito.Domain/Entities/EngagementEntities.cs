namespace Remito.Domain.Entities;

public sealed class Favourite
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public FavouriteType Type { get; set; }
    public Guid TargetId { get; set; }
    public string? TargetCode { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public User User { get; set; } = null!;
}

public enum FavouriteType
{
    Exchange,
    Corridor,
    Currency
}

public sealed class RedirectEvent
{
    public Guid Id { get; set; }
    public required string ClickId { get; set; }
    public Guid? UserId { get; set; }
    public Guid ExchangeId { get; set; }
    public Guid CorridorId { get; set; }
    public decimal Amount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public RedirectStatus Status { get; set; }
    public User? User { get; set; }
    public Exchange Exchange { get; set; } = null!;
    public Corridor Corridor { get; set; } = null!;
}

public enum RedirectStatus
{
    Created,
    Redirected,
    Failed
}

public sealed class ReferralConversion
{
    public Guid Id { get; set; }
    public Guid ExchangeId { get; set; }
    public required string ClickId { get; set; }
    public string? ExternalRef { get; set; }
    public DateTimeOffset ConvertedAt { get; set; }
    public decimal Amount { get; set; }
    public ConversionStatus Status { get; set; }
    public RedirectEvent RedirectEvent { get; set; } = null!;
}

public enum ConversionStatus
{
    Pending,
    Confirmed,
    Rejected
}

public sealed class CommissionLedger
{
    public Guid Id { get; set; }
    public Guid ConversionId { get; set; }
    public decimal Amount { get; set; }
    public required string CurrencyCode { get; set; }
    public LedgerStatus Status { get; set; }
    public ReferralConversion Conversion { get; set; } = null!;
    public Currency Currency { get; set; } = null!;
}

public sealed class PartnerReconciliationIssue
{
    public Guid Id { get; set; }
    public Guid ExchangeId { get; set; }
    public required string ExternalRef { get; set; }
    public string? ClickId { get; set; }
    public required string IssueType { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsResolved { get; set; }
}

public sealed class PartnerReportRow
{
    public Guid Id { get; set; }
    public Guid ExchangeId { get; set; }
    public required string ExternalRef { get; set; }
    public required string ClickId { get; set; }
    public DateTimeOffset ConvertedAt { get; set; }
    public decimal Amount { get; set; }
    public decimal Commission { get; set; }
    public required string CurrencyCode { get; set; }
}

public enum LedgerStatus
{
    Pending,
    Approved,
    Paid,
    Reversed
}
