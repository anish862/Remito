using System.Net;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Remito.Application.Engagement;
using Remito.Domain.Entities;
using Remito.Infrastructure.Persistence;

namespace Remito.Infrastructure.Engagement;

public sealed class RedirectService(
    RemitoDbContext dbContext,
    IConfiguration configuration,
    TimeProvider timeProvider) : IRedirectService
{
    private const string Disclosure =
        "Remito does not process payments. Transactions are completed on the chosen licensed exchange.";
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public async Task<RedirectResponse> CreateAsync(
        CreateRedirectRequest request,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        if (request.Amount <= 0 || request.Amount > 1_000_000_000_000m)
        {
            throw new EngagementOperationException(
                (int)HttpStatusCode.BadRequest,
                "Amount must be greater than zero and no more than one trillion.");
        }

        var pair = NormalizePair(request.Corridor);
        var exchange = await dbContext.Exchanges.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == request.ExchangeId, cancellationToken)
            ?? throw new EngagementOperationException((int)HttpStatusCode.NotFound, "Exchange not found.");
        if (exchange.Status != ExchangeStatus.Active || exchange.IsPlaceholder)
        {
            throw new EngagementOperationException((int)HttpStatusCode.NotFound, "Active exchange not found.");
        }

        if (userId.HasValue
            && !await dbContext.Users.AsNoTracking()
                .AnyAsync(user => user.Id == userId.Value && user.DeletedAt == null, cancellationToken))
        {
            userId = null;
        }

        var corridor = await dbContext.Corridors.AsNoTracking()
            .SingleOrDefaultAsync(item => item.FromCurrency == pair.From
                && item.ToCurrency == pair.To
                && item.IsActive, cancellationToken)
            ?? throw new EngagementOperationException((int)HttpStatusCode.NotFound, "Active corridor not found.");

        var signingKey = configuration["Redirect:SigningKey"];
        if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32)
        {
            throw new InvalidOperationException("Redirect:SigningKey must contain at least 32 bytes.");
        }

        var clickId = GenerateUlid(timeProvider.GetUtcNow());
        var destinationSignature = Sign(signingKey, clickId);
        var allowedHosts = ParseAllowedHosts(exchange.AllowedRedirectHosts);
        var gatewayUrl = BuildUrl(exchange.GatewayUrl, allowedHosts, clickId, destinationSignature, requireHttps: true);
        var storeUrl = BuildUrl(exchange.PlayStoreUrl, allowedHosts, clickId, destinationSignature, requireHttps: true);
        var fallbackUrl = storeUrl ?? gatewayUrl;
        var appUrl = BuildUrl(exchange.AppDeepLink, allowedHosts, clickId, destinationSignature, requireHttps: false);
        var destinationUrl = request.AppInstalledHint && exchange.HasApp && appUrl is not null
            ? appUrl
            : gatewayUrl ?? storeUrl
                ?? throw new EngagementOperationException(
                    (int)HttpStatusCode.Conflict,
                    "Exchange has no valid allow-listed destination.");

        dbContext.RedirectEvents.Add(new RedirectEvent
        {
            Id = Guid.NewGuid(),
            ClickId = clickId,
            UserId = userId,
            ExchangeId = exchange.Id,
            CorridorId = corridor.Id,
            Amount = request.Amount,
            CreatedAt = timeProvider.GetUtcNow(),
            Status = RedirectStatus.Redirected
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return new RedirectResponse(destinationUrl, fallbackUrl, clickId, Disclosure);
    }

    private static string? BuildUrl(
        string? configuredUrl,
        IReadOnlySet<string> allowedHosts,
        string clickId,
        string signature,
        bool requireHttps)
    {
        if (string.IsNullOrWhiteSpace(configuredUrl))
        {
            return null;
        }

        if (!Uri.TryCreate(configuredUrl, UriKind.Absolute, out var uri)
            || (requireHttps && uri.Scheme != Uri.UriSchemeHttps)
            || (!requireHttps
                && (uri.Scheme is "http" or "javascript" or "data" or "file" or "ftp"))
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !allowedHosts.Contains(uri.IdnHost)
            || HasUnsafeQueryKey(uri.Query))
        {
            throw new EngagementOperationException(
                (int)HttpStatusCode.Conflict,
                "Exchange destination is not on its configured allow-list.");
        }

        var builder = new UriBuilder(uri);
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(builder.Query))
        {
            query.Add(builder.Query.TrimStart('?'));
        }

        query.Add($"click_id={Uri.EscapeDataString(clickId)}");
        query.Add($"click_sig={Uri.EscapeDataString(signature)}");
        query.Add("utm_source=remito");
        query.Add("utm_medium=referral");
        query.Add("utm_campaign=exchange_redirect");
        builder.Query = string.Join("&", query);
        return builder.Uri.AbsoluteUri;
    }

    private static bool HasUnsafeQueryKey(string query)
    {
        foreach (var parameter in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = parameter.IndexOf('=');
            var key = separator < 0 ? parameter : parameter[..separator];
            key = Uri.UnescapeDataString(key.Replace('+', ' '));
            if (key.Equals("click_id", StringComparison.OrdinalIgnoreCase)
                || key.Equals("click_sig", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("utm_", StringComparison.OrdinalIgnoreCase)
                || key.Contains("name", StringComparison.OrdinalIgnoreCase)
                || key.Contains("phone", StringComparison.OrdinalIgnoreCase)
                || key.Contains("mobile", StringComparison.OrdinalIgnoreCase)
                || key.Contains("email", StringComparison.OrdinalIgnoreCase)
                || key.Contains("token", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static HashSet<string> ParseAllowedHosts(string? allowedHosts) =>
        (allowedHosts ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(host => host.Trim().TrimEnd('.').ToLowerInvariant())
            .Where(host => Uri.CheckHostName(host) is UriHostNameType.Dns or UriHostNameType.IPv4)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string Sign(string key, string value) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();

    private static string GenerateUlid(DateTimeOffset now)
    {
        var timestamp = checked((ulong)now.ToUnixTimeMilliseconds());
        Span<byte> random = stackalloc byte[10];
        RandomNumberGenerator.Fill(random);
        var value = (new BigInteger(timestamp) << 80) | new BigInteger(random, isUnsigned: true, isBigEndian: true);
        Span<char> result = stackalloc char[26];
        for (var index = result.Length - 1; index >= 0; index--)
        {
            result[index] = Alphabet[(int)(value & 31)];
            value >>= 5;
        }

        return new string(result);
    }

    private static (string From, string To) NormalizePair(string corridor)
    {
        var parts = corridor.Trim().ToUpperInvariant().Split(['-', '/'], StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || parts.Any(code =>
                code.Length != 3 || code.Any(character => character is < 'A' or > 'Z')))
        {
            throw new EngagementOperationException(
                (int)HttpStatusCode.BadRequest,
                "Corridor must use BASE-QUOTE format with ISO currency codes.");
        }

        return (parts[0], parts[1]);
    }
}
