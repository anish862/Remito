using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Remito.Domain.Entities;

namespace Remito.Infrastructure.Rates;

public sealed class MarketRateProvider(
    HttpClient httpClient,
    IOptions<MarketRateProviderOptions> options,
    TimeProvider timeProvider) : IRateProvider
{
    private readonly MarketRateProviderOptions settings = options.Value;

    public Guid SourceId => settings.SourceId;
    public string SourceName => settings.SourceName;
    public RateSourceType SourceType => RateSourceType.Provider;
    public TimeSpan StalenessThreshold => TimeSpan.FromMinutes(settings.StalenessMinutes);

    public async Task<IReadOnlyList<RateQuote>> FetchAsync(CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out var endpoint)
            || endpoint.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("The market-rate endpoint must be an absolute HTTPS URL.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            request.Headers.TryAddWithoutValidation(
                settings.ApiKeyHeader,
                $"{settings.ApiKeyPrefix}{settings.ApiKey}");
        }

        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var rows = RateJsonParser.ReadRows(document.RootElement);
        var fetchedAt = timeProvider.GetUtcNow();
        return rows.Select(row => new RateQuote(
                RateNormalizer.NormalizePair(row.Pair),
                row.Bid,
                row.Ask,
                row.Mid ?? (row.Bid + row.Ask) / 2m,
                SourceId,
                fetchedAt))
            .ToArray();
    }
}
