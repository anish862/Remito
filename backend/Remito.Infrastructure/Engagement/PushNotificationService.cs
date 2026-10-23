using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Remito.Application.Engagement;
using Remito.Domain.Entities;
using Remito.Infrastructure.Persistence;

namespace Remito.Infrastructure.Engagement;

public sealed class PushNotificationService(
    RemitoDbContext dbContext,
    IPushSender pushSender,
    TimeProvider timeProvider) : IPushNotificationService
{
    private static readonly HashSet<string> AllowedDataKeys =
        ["type", "alertId", "pair", "rate", "pairs", "slug"];

    public async Task<bool> SendToUserAsync(
        Guid userId,
        PushNotice notice,
        CancellationToken cancellationToken)
    {
        if (notice.Data.Keys.Any(key => !AllowedDataKeys.Contains(key))
            || notice.Data.Keys.Any(key =>
                key.Contains("name", StringComparison.OrdinalIgnoreCase)
                || key.Contains("email", StringComparison.OrdinalIgnoreCase)
                || key.Contains("phone", StringComparison.OrdinalIgnoreCase)
                || key.Contains("token", StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("Push data may contain only non-PII event fields.", nameof(notice));
        }

        var user = await dbContext.Users
            .Include(item => item.Devices)
            .SingleOrDefaultAsync(item => item.Id == userId && item.DeletedAt == null, cancellationToken);
        if (user is null)
        {
            return false;
        }

        if (notice.Category == "rate_alert" && !user.RateAlertsEnabled
            || notice.Category == "rate_alert_summary"
                && (!user.RateAlertsEnabled || !user.DailySummaryEnabled))
        {
            return false;
        }

        if (IsQuietNow(user, timeProvider.GetUtcNow()))
        {
            return false;
        }

        if (notice.Category == "marketing"
            && !await dbContext.UserConsents.AsNoTracking().AnyAsync(
                consent => consent.UserId == userId
                    && consent.Type == ConsentType.Marketing
                    && consent.IsGranted,
                cancellationToken))
        {
            return false;
        }

        var devices = user.Devices.ToArray();
        if (devices.Length == 0)
        {
            return false;
        }

        var sendResult = await pushSender.SendAsync(
            devices.Select(device => device.FcmToken).ToArray(),
            notice,
            cancellationToken);
        if (sendResult.InvalidTokens.Count > 0)
        {
            var invalidHashes = sendResult.InvalidTokens
                .Select(HashToken)
                .ToHashSet(StringComparer.Ordinal);
            dbContext.Devices.RemoveRange(devices.Where(device => invalidHashes.Contains(device.FcmTokenHash)));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    private static bool IsQuietNow(User user, DateTimeOffset now)
    {
        if (user.QuietHoursStartMinute is not { } start
            || user.QuietHoursEndMinute is not { } end
            || start == end)
        {
            return false;
        }

        var minute = now.Hour * 60 + now.Minute;
        return start < end
            ? minute >= start && minute < end
            : minute >= start || minute < end;
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
