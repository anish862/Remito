using Remito.Domain.Entities;

namespace Remito.Application.Engagement;

/// <summary>Input to create an exchange redirect.</summary>
public sealed record CreateRedirectRequest(Guid ExchangeId, string Corridor, decimal Amount, bool AppInstalledHint = false);

/// <summary>Safe redirect URLs and the generated attribution identifier.</summary>
public sealed record RedirectResponse(
    string DestinationUrl,
    string? FallbackUrl,
    string ClickId,
    string DisclosureText,
    string? GatewayUrl = null);

/// <summary>Partner-reported conversion payload.</summary>
public sealed record PartnerPostbackRequest(
    string ClickId,
    string ExternalRef,
    DateTimeOffset ConvertedAt,
    decimal Amount,
    ConversionStatus Status = ConversionStatus.Confirmed);

/// <summary>Result of processing a partner conversion postback.</summary>
public sealed record PartnerPostbackResponse(bool Accepted, bool WasDuplicate, Guid? ConversionId);

/// <summary>Monthly exchange attribution and commission totals.</summary>
public sealed record ExchangeMonthlyReport(
    Guid ExchangeId,
    string ExchangeName,
    DateOnly Month,
    long Redirects,
    long Conversions,
    decimal ConversionRatePercent,
    decimal Commission,
    string Currency,
    decimal ReportedCommission,
    decimal CommissionVariance,
    long UnmatchedReportRows,
    long MissingPostbacks);

/// <summary>Creates privacy-safe redirects.</summary>
public interface IRedirectService
{
    Task<RedirectResponse> CreateAsync(
        CreateRedirectRequest request,
        Guid? userId,
        CancellationToken cancellationToken);
}

/// <summary>Verifies partner postbacks and manages attribution reports.</summary>
public interface IPartnerAttributionService
{
    Task<PartnerPostbackResponse> ProcessPostbackAsync(
        Guid exchangeId,
        string timestampHeader,
        string signatureHeader,
        string rawBody,
        PartnerPostbackRequest request,
        CancellationToken cancellationToken);

    Task<ExchangeMonthlyReport> GetMonthlyReportAsync(
        Guid exchangeId,
        int year,
        int month,
        CancellationToken cancellationToken);
}

/// <summary>Imports partner CSV reports and reconciles their conversion rows.</summary>
public interface IPartnerReconciliationJob
{
    Task RunAsync(CancellationToken cancellationToken);
}

/// <summary>Favourite item returned to its owner.</summary>
public sealed record FavouriteResponse(Guid Id, FavouriteType Type, Guid TargetId, string? TargetCode, DateTimeOffset CreatedAt);

/// <summary>Input used to add a currency, corridor, or exchange favourite.</summary>
public sealed record CreateFavouriteRequest(FavouriteType Type, Guid? TargetId, string? CurrencyCode);

/// <summary>Creates and removes the authenticated user's favourites.</summary>
public interface IFavouriteService
{
    Task<IReadOnlyList<FavouriteResponse>> ListAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken);
    Task<FavouriteResponse> AddAsync(Guid userId, CreateFavouriteRequest request, CancellationToken cancellationToken);
    Task RemoveAsync(Guid userId, Guid favouriteId, CancellationToken cancellationToken);
}

/// <summary>Rate alert returned to its owner.</summary>
public sealed record AlertResponse(
    Guid Id,
    string Pair,
    AlertDirection Direction,
    decimal Target,
    bool IsActive,
    bool IsOneTime,
    bool DailySummary,
    DateTimeOffset? LastTriggeredAt);

/// <summary>Creates or updates a rate threshold alert.</summary>
public sealed record SaveAlertRequest(
    string Pair,
    AlertDirection Direction,
    decimal Target,
    bool IsActive = true,
    bool IsOneTime = false,
    bool DailySummary = false);

/// <summary>Manages a user's rate alerts.</summary>
public interface IAlertService
{
    Task<IReadOnlyList<AlertResponse>> ListAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken);
    Task<AlertResponse> CreateAsync(Guid userId, SaveAlertRequest request, CancellationToken cancellationToken);
    Task<AlertResponse> UpdateAsync(Guid userId, Guid alertId, SaveAlertRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid userId, Guid alertId, CancellationToken cancellationToken);
}

/// <summary>Notification delivery preferences returned to an account owner.</summary>
public sealed record NotificationPreferencesResponse(
    bool RateAlertsEnabled,
    bool DailySummaryEnabled,
    int? QuietHoursStartMinute,
    int? QuietHoursEndMinute);

/// <summary>Changes notification preferences and UTC quiet-hour bounds.</summary>
public sealed record UpdateNotificationPreferencesRequest(
    bool RateAlertsEnabled,
    bool DailySummaryEnabled,
    int? QuietHoursStartMinute,
    int? QuietHoursEndMinute);

/// <summary>Reads and updates a user's notification preferences.</summary>
public interface INotificationPreferencesService
{
    Task<NotificationPreferencesResponse> GetAsync(Guid userId, CancellationToken cancellationToken);
    Task<NotificationPreferencesResponse> UpdateAsync(
        Guid userId,
        UpdateNotificationPreferencesRequest request,
        CancellationToken cancellationToken);
}

/// <summary>A non-PII notification payload for a device.</summary>
public sealed record PushNotice(string Title, string Body, IReadOnlyDictionary<string, string> Data, string Category);

/// <summary>Delivers a notification batch and identifies tokens rejected as invalid.</summary>
public sealed record PushBatchResult(IReadOnlySet<string> InvalidTokens);

/// <summary>Sends notification batches through the configured push provider.</summary>
public interface IPushSender
{
    Task<PushBatchResult> SendAsync(
        IReadOnlyList<string> tokens,
        PushNotice notice,
        CancellationToken cancellationToken);
}

/// <summary>Applies account status, consent, and preference rules before push delivery.</summary>
public interface IPushNotificationService
{
    Task<bool> SendToUserAsync(Guid userId, PushNotice notice, CancellationToken cancellationToken);
}

/// <summary>Runs one evaluation pass over active user rate alerts.</summary>
public interface IRateAlertEvaluationJob
{
    Task RunAsync(CancellationToken cancellationToken);
}

/// <summary>Public news content returned to API clients.</summary>
public sealed record NewsResponse(
    Guid Id,
    string Title,
    string Slug,
    string Summary,
    string Body,
    string? SourceUrl,
    DateTimeOffset? PublishedAt);

/// <summary>Admin-curated news input.</summary>
public sealed record SaveNewsRequest(
    string Title,
    string Slug,
    string Summary,
    string Body,
    string? SourceUrl,
    bool IsPublished);

/// <summary>Reads public news and manages curated content.</summary>
public interface INewsService
{
    Task<IReadOnlyList<NewsResponse>> ListAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<IReadOnlyList<NewsResponse>> ListAdminAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<NewsResponse> GetAsync(string slug, CancellationToken cancellationToken);
    Task<NewsResponse> SaveAsync(Guid? id, SaveNewsRequest request, CancellationToken cancellationToken);
}

/// <summary>Imports configured RSS or Atom stories into curated news.</summary>
public interface IRssNewsImportJob
{
    Task RunAsync(CancellationToken cancellationToken);
}

/// <summary>Represents an invalid engagement request or unavailable resource.</summary>
public sealed class EngagementOperationException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
