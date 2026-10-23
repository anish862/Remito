using System.Globalization;
using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Remito.Application.Engagement;
using Remito.Domain.Entities;
using Remito.Infrastructure.Persistence;

namespace Remito.Infrastructure.Engagement;

public sealed class NewsRssImportJob(
    HttpClient httpClient,
    RemitoDbContext dbContext,
    IConfiguration configuration,
    TimeProvider timeProvider,
    ILogger<NewsRssImportJob> logger) : IRssNewsImportJob
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var feedUrl = configuration["News:Rss:FeedUrl"];
        if (string.IsNullOrWhiteSpace(feedUrl))
        {
            return;
        }

        if (!Uri.TryCreate(feedUrl, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new InvalidOperationException("News RSS feed URL must be an absolute HTTPS URL.");
        }

        using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode is >= HttpStatusCode.MovedPermanently and <= HttpStatusCode.TemporaryRedirect)
        {
            throw new InvalidOperationException("News RSS feed redirects are not allowed.");
        }

        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > 2_000_000)
        {
            throw new InvalidDataException("News RSS feed exceeds the 2 MB limit.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 2_000_000
        };
        using var reader = XmlReader.Create(stream, settings);
        var document = await XDocument.LoadAsync(reader, LoadOptions.None, cancellationToken);
        var entries = document.Descendants()
            .Where(element => element.Name.LocalName is "item" or "entry")
            .Take(25)
            .ToArray();
        foreach (var entry in entries)
        {
            var title = Text(entry, "title");
            var body = Text(entry, "description", "summary", "content");
            var sourceUrl = GetLink(entry);
            if (string.IsNullOrWhiteSpace(title)
                || string.IsNullOrWhiteSpace(body)
                || !Uri.TryCreate(sourceUrl, UriKind.Absolute, out var source)
                || source.Scheme != Uri.UriSchemeHttps
                || !string.IsNullOrEmpty(source.UserInfo))
            {
                continue;
            }

            var slug = CreateSlug(title);
            if (slug.Length == 0
                || await dbContext.NewsItems.AnyAsync(item => item.Slug == slug, cancellationToken))
            {
                continue;
            }

            var publishedAt = DateTimeOffset.TryParse(
                Text(entry, "pubDate", "published", "updated"),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var parsedPublishedAt)
                ? parsedPublishedAt.ToUniversalTime()
                : timeProvider.GetUtcNow();
            dbContext.NewsItems.Add(new NewsItem
            {
                Id = Guid.NewGuid(),
                Title = title[..Math.Min(title.Length, 300)],
                Slug = slug[..Math.Min(slug.Length, 300)],
                Summary = body[..Math.Min(body.Length, 1000)],
                Body = body[..Math.Min(body.Length, 100_000)],
                SourceUrl = source.AbsoluteUri,
                PublishedAt = publishedAt,
                IsPublished = true
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("News RSS import completed. EntryCount={EntryCount}", entries.Length);
    }

    private static string Text(XElement element, params string[] names) =>
        element.Elements()
            .FirstOrDefault(child => names.Contains(child.Name.LocalName, StringComparer.OrdinalIgnoreCase))
            ?.Value.Trim() ?? string.Empty;

    private static string? GetLink(XElement entry)
    {
        var link = entry.Elements().FirstOrDefault(element =>
            string.Equals(element.Name.LocalName, "link", StringComparison.OrdinalIgnoreCase));
        return link is null
            ? null
            : link.Attribute("href")?.Value ?? link.Value.Trim();
    }

    private static string CreateSlug(string title)
    {
        var builder = new StringBuilder(title.Length);
        var pendingDash = false;
        foreach (var character in title.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                if (pendingDash && builder.Length > 0)
                {
                    builder.Append('-');
                }

                builder.Append(character);
                pendingDash = false;
            }
            else
            {
                pendingDash = true;
            }
        }

        return builder.ToString();
    }
}
