using System.Text.Json;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Hangfire;
using Remito.Application.Abstractions;
using Remito.Application.Engagement;
using Remito.Domain.Entities;
using Remito.Infrastructure.Persistence;

namespace Remito.Infrastructure.Engagement;

public sealed class RateAlertEvaluationJob(
    RemitoDbContext dbContext,
    IRateCache rateCache,
    IPushNotificationService pushNotificationService,
    TimeProvider timeProvider,
    ILogger<RateAlertEvaluationJob> logger) : IRateAlertEvaluationJob
{
    private static readonly JsonSerializerOptions CacheJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [DisableConcurrentExecution(timeoutInSeconds: 55)]
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var alerts = await dbContext.Alerts
            .Include(alert => alert.User)
            .ThenInclude(user => user.Devices)
            .Where(alert => alert.IsActive && alert.User.DeletedAt == null)
            .OrderBy(alert => alert.UserId)
            .ThenBy(alert => alert.Id)
            .ToListAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var changed = false;
        var triggered = new List<(Alert Alert, decimal Rate)>();
        foreach (var alert in alerts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var quote = await ReadRateAsync(alert.Pair, cancellationToken);
            if (quote is null || quote.IsStale
                || now - quote.FetchedAt > TimeSpan.FromMinutes(Math.Max(1, quote.StalenessMinutes)))
            {
                continue;
            }

            var isCrossed = alert.Direction == AlertDirection.Above
                ? quote.Mid >= alert.Target
                : quote.Mid <= alert.Target;
            if (!alert.IsArmed)
            {
                if (!isCrossed)
                {
                    alert.IsArmed = true;
                    changed = true;
                }

                continue;
            }

            if (!isCrossed)
            {
                continue;
            }

            alert.IsArmed = false;
            alert.LastTriggeredAt = now;
            if (alert.IsOneTime)
            {
                alert.IsActive = false;
            }

            triggered.Add((alert, quote.Mid));
            changed = true;
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        foreach (var (alert, rate) in triggered)
        {
            if (!alert.User.RateAlertsEnabled
                || (alert.DailySummary && alert.User.DailySummaryEnabled))
            {
                continue;
            }

            await pushNotificationService.SendToUserAsync(
                alert.UserId,
                new PushNotice(
                    "Rate alert",
                    $"{alert.Pair} crossed your alert threshold.",
                    new Dictionary<string, string>
                    {
                        ["type"] = "rate_alert",
                        ["alertId"] = alert.Id.ToString("D"),
                        ["pair"] = alert.Pair,
                        ["rate"] = rate.ToString(CultureInfo.InvariantCulture)
                    },
                    "rate_alert"),
                cancellationToken);
        }
    }

    [DisableConcurrentExecution(timeoutInSeconds: 55)]
    public async Task RunDailySummaryAsync(CancellationToken cancellationToken)
    {
        var users = await dbContext.Users
            .Include(user => user.Devices)
            .Where(user => user.DeletedAt == null
                && user.RateAlertsEnabled
                && user.DailySummaryEnabled)
            .ToListAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        foreach (var user in users)
        {
            var since = user.LastAlertSummaryAt ?? DateTimeOffset.MinValue;
            var userAlerts = await dbContext.Alerts.AsNoTracking()
                .Where(alert => alert.UserId == user.Id && alert.DailySummary)
                .ToListAsync(cancellationToken);
            var pairs = userAlerts
                .Where(alert => alert.LastTriggeredAt > since && alert.LastTriggeredAt <= now)
                .Select(alert => alert.Pair)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (pairs.Length == 0)
            {
                continue;
            }

            var sent = await pushNotificationService.SendToUserAsync(
                user.Id,
                new PushNotice(
                    "Your rate alert summary",
                    $"Rate thresholds crossed: {string.Join(", ", pairs)}.",
                    new Dictionary<string, string>
                    {
                        ["type"] = "rate_alert_summary",
                        ["pairs"] = string.Join(",", pairs)
                    },
                    "rate_alert_summary"),
                cancellationToken);
            if (sent)
            {
                user.LastAlertSummaryAt = now;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<CachedRate?> ReadRateAsync(string pair, CancellationToken cancellationToken)
    {
        try
        {
            var cached = await rateCache.GetAsync(
                $"rates:latest:{pair.Replace("/", "-", StringComparison.Ordinal)}",
                cancellationToken);
            return cached is null ? null : JsonSerializer.Deserialize<CachedRate>(cached, CacheJsonOptions);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Rate alert cache entry is invalid. Pair={Pair}", pair);
            return null;
        }
    }

    private sealed record CachedRate(decimal Mid, DateTimeOffset FetchedAt, bool IsStale, int StalenessMinutes);
}
