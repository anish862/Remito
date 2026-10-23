using System.Net;
using Microsoft.EntityFrameworkCore;
using Remito.Application.Engagement;
using Remito.Domain.Entities;
using Remito.Infrastructure.Persistence;

namespace Remito.Infrastructure.Engagement;

public sealed class AlertService(
    RemitoDbContext dbContext) : IAlertService
{
    public async Task<IReadOnlyList<AlertResponse>> ListAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        ValidatePage(page, pageSize);
        await EnsureActiveUserAsync(userId, cancellationToken);
        return await dbContext.Alerts.AsNoTracking()
            .Where(alert => alert.UserId == userId)
            .OrderByDescending(alert => alert.LastTriggeredAt)
            .ThenBy(alert => alert.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToResponseExpression())
            .ToListAsync(cancellationToken);
    }

    public async Task<AlertResponse> CreateAsync(
        Guid userId,
        SaveAlertRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureActiveUserAsync(userId, cancellationToken);
        var pair = await ValidateRequestAsync(request, cancellationToken);
        var alert = new Alert
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Pair = pair,
            Direction = request.Direction,
            Target = request.Target,
            IsActive = request.IsActive,
            IsOneTime = request.IsOneTime,
            DailySummary = request.DailySummary,
            IsArmed = true
        };
        dbContext.Alerts.Add(alert);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(alert);
    }

    public async Task<AlertResponse> UpdateAsync(
        Guid userId,
        Guid alertId,
        SaveAlertRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureActiveUserAsync(userId, cancellationToken);
        var pair = await ValidateRequestAsync(request, cancellationToken);
        var alert = await dbContext.Alerts.SingleOrDefaultAsync(
            item => item.Id == alertId && item.UserId == userId,
            cancellationToken)
            ?? throw new EngagementOperationException((int)HttpStatusCode.NotFound, "Alert not found.");
        alert.Pair = pair;
        alert.Direction = request.Direction;
        alert.Target = request.Target;
        alert.IsActive = request.IsActive;
        alert.IsOneTime = request.IsOneTime;
        alert.DailySummary = request.DailySummary;
        alert.IsArmed = true;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(alert);
    }

    public async Task DeleteAsync(Guid userId, Guid alertId, CancellationToken cancellationToken)
    {
        await EnsureActiveUserAsync(userId, cancellationToken);
        var alert = await dbContext.Alerts.SingleOrDefaultAsync(
            item => item.Id == alertId && item.UserId == userId,
            cancellationToken)
            ?? throw new EngagementOperationException((int)HttpStatusCode.NotFound, "Alert not found.");
        dbContext.Alerts.Remove(alert);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<string> ValidateRequestAsync(SaveAlertRequest request, CancellationToken cancellationToken)
    {
        if (request.Target <= 0)
        {
            throw new EngagementOperationException((int)HttpStatusCode.BadRequest, "Alert target must be positive.");
        }

        var parts = request.Pair.Trim().ToUpperInvariant().Split(['-', '/'], StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || parts.Any(code =>
                code.Length != 3 || code.Any(character => character is < 'A' or > 'Z')))
        {
            throw new EngagementOperationException((int)HttpStatusCode.BadRequest, "Pair must use BASE-QUOTE format.");
        }

        var pair = $"{parts[0]}/{parts[1]}";
        var currencyCodes = await dbContext.Currencies.AsNoTracking()
            .Where(currency => parts.Contains(currency.Code))
            .Select(currency => currency.Code)
            .ToListAsync(cancellationToken);
        if (currencyCodes.Count != 2)
        {
            throw new EngagementOperationException((int)HttpStatusCode.BadRequest, "Pair contains an unknown currency.");
        }

        return pair;
    }

    private async Task EnsureActiveUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Users.AnyAsync(user => user.Id == userId && user.DeletedAt == null, cancellationToken))
        {
            throw new EngagementOperationException((int)HttpStatusCode.Unauthorized, "Account is unavailable.");
        }
    }

    private static System.Linq.Expressions.Expression<Func<Alert, AlertResponse>> ToResponseExpression() =>
        alert => new AlertResponse(
            alert.Id,
            alert.Pair,
            alert.Direction,
            alert.Target,
            alert.IsActive,
            alert.IsOneTime,
            alert.DailySummary,
            alert.LastTriggeredAt);

    private static AlertResponse ToResponse(Alert alert) =>
        new(alert.Id, alert.Pair, alert.Direction, alert.Target, alert.IsActive,
            alert.IsOneTime, alert.DailySummary, alert.LastTriggeredAt);

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100)
        {
            throw new EngagementOperationException(
                (int)HttpStatusCode.BadRequest,
                "Page must be positive and pageSize must be between 1 and 100.");
        }
    }
}
