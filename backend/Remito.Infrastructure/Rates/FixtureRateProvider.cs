using System.Text.Json;
using Microsoft.Extensions.Options;
using Remito.Domain.Entities;

namespace Remito.Infrastructure.Rates;

public sealed class FixtureRateProvider(
    IOptions<FixtureRateProviderOptions> options,
    TimeProvider timeProvider) : IRateProvider
{
    private readonly FixtureRateProviderOptions settings = options.Value;

    public Guid SourceId => settings.SourceId;
    public string SourceName => settings.SourceName;
    public RateSourceType SourceType => RateSourceType.Provider;
    public TimeSpan StalenessThreshold => TimeSpan.FromMinutes(settings.StalenessMinutes);

    public async Task<IReadOnlyList<RateQuote>> FetchAsync(CancellationToken cancellationToken)
    {
        var path = Path.IsPathRooted(settings.Path)
            ? settings.Path
            : Path.Combine(AppContext.BaseDirectory, settings.Path);
        await using var stream = File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var fetchedAt = timeProvider.GetUtcNow();
        return RateJsonParser.ReadRows(document.RootElement)
            .Select(row => new RateQuote(
                RateNormalizer.NormalizePair(row.Pair),
                row.Bid,
                row.Ask,
                row.Mid ?? (row.Bid + row.Ask) / 2m,
                SourceId,
                fetchedAt))
            .ToArray();
    }
}
