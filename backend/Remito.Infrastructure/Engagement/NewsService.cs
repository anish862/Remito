using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Remito.Application.Abstractions;
using Remito.Application.Engagement;
using Remito.Domain.Entities;
using Remito.Infrastructure.Persistence;

namespace Remito.Infrastructure.Engagement;

public sealed class NewsService(
    RemitoDbContext dbContext,
    IRateCache rateCache,
    TimeProvider timeProvider,
    ILogger<NewsService> logger) : INewsService
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(60);

    public async Task<IReadOnlyList<NewsResponse>> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        ValidatePage(page, pageSize);
        var cacheKey = $"news:published:{page}:{pageSize}";
        try
        {
            var value = await rateCache.GetAsync(cacheKey, cancellationToken);
            if (value is not null)
            {
                var cached = JsonSerializer.Deserialize<NewsResponse[]>(value);
                if (cached is not null)
                {
                    return cached;
                }
            }
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "News cache entry is invalid. Page={Page}", page);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "News cache read failed. Page={Page}", page);
        }

        var news = await dbContext.NewsItems.AsNoTracking()
            .Where(item => item.IsPublished && item.PublishedAt != null)
            .OrderByDescending(item => item.PublishedAt)
            .ThenBy(item => item.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new NewsResponse(
                item.Id,
                item.Title,
                item.Slug,
                item.Summary,
                item.Body,
                item.SourceUrl,
                item.PublishedAt!.Value))
            .ToListAsync(cancellationToken);
        try
        {
            await rateCache.SetAsync(cacheKey, JsonSerializer.Serialize(news), CacheLifetime, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "News cache write failed. Page={Page}", page);
        }

        return news;
    }

    public async Task<NewsResponse> GetAsync(string slug, CancellationToken cancellationToken)
    {
        var item = await dbContext.NewsItems.AsNoTracking()
            .SingleOrDefaultAsync(news => news.Slug == slug
                && news.IsPublished
                && news.PublishedAt != null,
                cancellationToken)
            ?? throw new EngagementOperationException((int)HttpStatusCode.NotFound, "News item not found.");
        return ToResponse(item);
    }

    public async Task<IReadOnlyList<NewsResponse>> ListAdminAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        ValidatePage(page, pageSize);
        return await dbContext.NewsItems.AsNoTracking()
            .OrderByDescending(item => item.PublishedAt)
            .ThenBy(item => item.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new NewsResponse(
                item.Id,
                item.Title,
                item.Slug,
                item.Summary,
                item.Body,
                item.SourceUrl,
                item.PublishedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<NewsResponse> SaveAsync(
        Guid? id,
        SaveNewsRequest request,
        CancellationToken cancellationToken)
    {
        ValidateNewsRequest(request);
        var item = id.HasValue
            ? await dbContext.NewsItems.SingleOrDefaultAsync(news => news.Id == id.Value, cancellationToken)
                ?? throw new EngagementOperationException((int)HttpStatusCode.NotFound, "News item not found.")
            : new NewsItem
            {
                Id = Guid.NewGuid(),
                Title = request.Title.Trim(),
                Slug = request.Slug.Trim(),
                Summary = request.Summary.Trim(),
                Body = request.Body.Trim()
            };
        item.Title = request.Title.Trim();
        item.Slug = request.Slug.Trim().ToLowerInvariant();
        item.Summary = request.Summary.Trim();
        item.Body = request.Body.Trim();
        item.SourceUrl = request.SourceUrl?.Trim();
        item.IsPublished = request.IsPublished;
        item.PublishedAt = request.IsPublished
            ? item.PublishedAt ?? timeProvider.GetUtcNow()
            : null;
        if (!id.HasValue)
        {
            dbContext.NewsItems.Add(item);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(item);
    }

    private static void ValidateNewsRequest(SaveNewsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 300
            || string.IsNullOrWhiteSpace(request.Slug) || request.Slug.Length > 300
            || request.Slug.Any(character => !(char.IsAsciiLetterOrDigit(character) || character == '-'))
            || string.IsNullOrWhiteSpace(request.Summary) || request.Summary.Length > 1000
            || string.IsNullOrWhiteSpace(request.Body) || request.Body.Length > 100_000)
        {
            throw new EngagementOperationException(
                (int)HttpStatusCode.BadRequest,
                "News content or slug is invalid.");
        }

        if (request.SourceUrl is not null
            && (!Uri.TryCreate(request.SourceUrl, UriKind.Absolute, out var source)
                || source.Scheme != Uri.UriSchemeHttps
                || !string.IsNullOrEmpty(source.UserInfo)))
        {
            throw new EngagementOperationException(
                (int)HttpStatusCode.BadRequest,
                "News source URL must be an absolute HTTPS URL.");
        }
    }

    private static NewsResponse ToResponse(NewsItem item) =>
        new(item.Id, item.Title, item.Slug, item.Summary, item.Body, item.SourceUrl, item.PublishedAt);

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
