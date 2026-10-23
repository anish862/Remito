using System.Net;
using Microsoft.EntityFrameworkCore;
using Remito.Application.Engagement;
using Remito.Infrastructure.Persistence;

namespace Remito.Infrastructure.Engagement;

public sealed class NotificationPreferencesService(RemitoDbContext dbContext) : INotificationPreferencesService
{
    public async Task<NotificationPreferencesResponse> GetAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == userId && item.DeletedAt == null, cancellationToken)
            ?? throw new EngagementOperationException((int)HttpStatusCode.Unauthorized, "Account is unavailable.");
        return ToResponse(user);
    }

    public async Task<NotificationPreferencesResponse> UpdateAsync(
        Guid userId,
        UpdateNotificationPreferencesRequest request,
        CancellationToken cancellationToken)
    {
        if (request.QuietHoursStartMinute is < 0 or > 1439
            || request.QuietHoursEndMinute is < 0 or > 1439
            || request.QuietHoursStartMinute.HasValue != request.QuietHoursEndMinute.HasValue)
        {
            throw new EngagementOperationException(
                (int)HttpStatusCode.BadRequest,
                "Quiet hours must include both UTC minute values between 0 and 1439.");
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(
            item => item.Id == userId && item.DeletedAt == null,
            cancellationToken)
            ?? throw new EngagementOperationException((int)HttpStatusCode.Unauthorized, "Account is unavailable.");
        user.RateAlertsEnabled = request.RateAlertsEnabled;
        user.DailySummaryEnabled = request.DailySummaryEnabled;
        user.QuietHoursStartMinute = request.QuietHoursStartMinute;
        user.QuietHoursEndMinute = request.QuietHoursEndMinute;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(user);
    }

    private static NotificationPreferencesResponse ToResponse(Domain.Entities.User user) =>
        new(user.RateAlertsEnabled, user.DailySummaryEnabled,
            user.QuietHoursStartMinute, user.QuietHoursEndMinute);
}
