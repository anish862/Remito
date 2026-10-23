using System.Net;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Remito.Application.Engagement;
using Remito.Domain.Entities;
using Remito.Infrastructure.Persistence;

namespace Remito.Infrastructure.Engagement;

public sealed class FavouriteService(
    RemitoDbContext dbContext,
    TimeProvider timeProvider) : IFavouriteService
{
    public async Task<IReadOnlyList<FavouriteResponse>> ListAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        ValidatePage(page, pageSize);
        await EnsureActiveUserAsync(userId, cancellationToken);
        return await dbContext.Favourites.AsNoTracking()
            .Where(favourite => favourite.UserId == userId)
            .OrderByDescending(favourite => favourite.CreatedAt)
            .ThenBy(favourite => favourite.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(favourite => new FavouriteResponse(
                favourite.Id,
                favourite.Type,
                favourite.TargetId,
                favourite.TargetCode,
                favourite.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<FavouriteResponse> AddAsync(
        Guid userId,
        CreateFavouriteRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureActiveUserAsync(userId, cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var (targetId, targetCode) = await ResolveTargetAsync(request, cancellationToken);
        var existing = await dbContext.Favourites.SingleOrDefaultAsync(
            favourite => favourite.UserId == userId
                && favourite.Type == request.Type
                && favourite.TargetId == targetId
                && favourite.TargetCode == targetCode,
            cancellationToken);
        if (existing is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return ToResponse(existing);
        }

        if (await dbContext.Favourites.CountAsync(favourite => favourite.UserId == userId, cancellationToken) >= 50)
        {
            throw new EngagementOperationException(
                (int)HttpStatusCode.Conflict,
                "An account can have at most 50 favourites.");
        }

        var favourite = new Favourite
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = request.Type,
            TargetId = targetId,
            TargetCode = targetCode,
            CreatedAt = timeProvider.GetUtcNow()
        };
        dbContext.Favourites.Add(favourite);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(favourite);
    }

    public async Task RemoveAsync(Guid userId, Guid favouriteId, CancellationToken cancellationToken)
    {
        await EnsureActiveUserAsync(userId, cancellationToken);
        var favourite = await dbContext.Favourites.SingleOrDefaultAsync(
            item => item.Id == favouriteId && item.UserId == userId,
            cancellationToken)
            ?? throw new EngagementOperationException((int)HttpStatusCode.NotFound, "Favourite not found.");
        dbContext.Favourites.Remove(favourite);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<(Guid TargetId, string? TargetCode)> ResolveTargetAsync(
        CreateFavouriteRequest request,
        CancellationToken cancellationToken)
    {
        switch (request.Type)
        {
            case FavouriteType.Currency:
            {
                var code = request.CurrencyCode?.Trim().ToUpperInvariant();
                if (code is null || code.Length != 3 || code.Any(character => character is < 'A' or > 'Z')
                    || !await dbContext.Currencies.AnyAsync(item => item.Code == code, cancellationToken))
                {
                    throw new EngagementOperationException((int)HttpStatusCode.BadRequest, "Currency code is invalid.");
                }

                return (Guid.Empty, code);
            }
            case FavouriteType.Exchange:
                if (!request.TargetId.HasValue
                    || !await dbContext.Exchanges.AnyAsync(
                        item => item.Id == request.TargetId.Value && !item.IsPlaceholder,
                        cancellationToken))
                {
                    throw new EngagementOperationException((int)HttpStatusCode.BadRequest, "Exchange is invalid.");
                }

                return (request.TargetId.Value, null);
            case FavouriteType.Corridor:
                if (!request.TargetId.HasValue
                    || !await dbContext.Corridors.AnyAsync(
                        item => item.Id == request.TargetId.Value,
                        cancellationToken))
                {
                    throw new EngagementOperationException((int)HttpStatusCode.BadRequest, "Corridor is invalid.");
                }

                return (request.TargetId.Value, null);
            default:
                throw new EngagementOperationException((int)HttpStatusCode.BadRequest, "Favourite type is invalid.");
        }
    }

    private async Task EnsureActiveUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Users.AnyAsync(
                user => user.Id == userId && user.DeletedAt == null,
                cancellationToken))
        {
            throw new EngagementOperationException((int)HttpStatusCode.Unauthorized, "Account is unavailable.");
        }
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100)
        {
            throw new EngagementOperationException(
                (int)HttpStatusCode.BadRequest,
                "Page must be positive and pageSize must be between 1 and 100.");
        }
    }

    private static FavouriteResponse ToResponse(Favourite item) =>
        new(item.Id, item.Type, item.TargetId, item.TargetCode, item.CreatedAt);
}
