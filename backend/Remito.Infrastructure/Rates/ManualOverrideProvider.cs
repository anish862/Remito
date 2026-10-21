using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Remito.Domain.Entities;
using Remito.Infrastructure.Persistence;

namespace Remito.Infrastructure.Rates;

public sealed class ManualRateOverrideProvider(
    RemitoDbContext dbContext,
    IOptions<ManualOverrideOptions> options,
    TimeProvider timeProvider) : IRateProvider
{
    private readonly ManualOverrideOptions settings = options.Value;

    public Guid SourceId => settings.SourceId;
    public string SourceName => settings.SourceName;
    public RateSourceType SourceType => RateSourceType.Manual;
    public TimeSpan StalenessThreshold => TimeSpan.FromMinutes(settings.StalenessMinutes);

    public async Task<IReadOnlyList<RateQuote>> FetchAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var candidates = await dbContext.ManualRateOverrides.AsNoTracking()
            .Where(rateOverride => rateOverride.SourceId == SourceId)
            .ToListAsync(cancellationToken);
        var overrides = candidates
            .Where(rateOverride => rateOverride.ExpiresAt > now)
            .GroupBy(rateOverride => rateOverride.Pair)
            .Select(group => group.MaxBy(rateOverride => rateOverride.CreatedAt)!)
            .ToArray();
        return overrides.Select(rateOverride => new RateQuote(
            rateOverride.Pair,
            rateOverride.Bid,
            rateOverride.Ask,
            rateOverride.Mid,
            SourceId,
            now)).ToArray();
    }
}

public sealed class ManualRateOverrideService(
    RemitoDbContext dbContext,
    IOptions<ManualOverrideOptions> options,
    TimeProvider timeProvider) : IManualRateOverrideService
{
    private readonly ManualOverrideOptions settings = options.Value;

    public async Task<Guid> CreateAsync(
        ManualRateOverrideRequest request,
        CancellationToken cancellationToken)
    {
        var pair = RateNormalizer.NormalizePair(request.Pair);
        if (request.Bid <= 0 || request.Ask < request.Bid)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Manual rates must have a positive bid no greater than ask.");
        }

        var reason = request.Reason.Trim();
        if (reason.Length is < 5 or > 500)
        {
            throw new ArgumentException("The manual rate reason must contain 5 to 500 characters.", nameof(request));
        }

        var now = timeProvider.GetUtcNow();
        if (request.ExpiresAt <= now || request.ExpiresAt > now.AddDays(30))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Manual rates must expire within the next 30 days.");
        }

        var source = await dbContext.RateSources
            .FirstOrDefaultAsync(candidate => candidate.Id == settings.SourceId, cancellationToken);
        if (source is null)
        {
            dbContext.RateSources.Add(new RateSource
            {
                Id = settings.SourceId,
                Name = settings.SourceName,
                Type = RateSourceType.Manual,
                Priority = 0,
                IsActive = true
            });
        }

        var rateOverride = new ManualRateOverride
        {
            Id = Guid.NewGuid(),
            Pair = pair,
            SourceId = settings.SourceId,
            Bid = request.Bid,
            Ask = request.Ask,
            Mid = (request.Bid + request.Ask) / 2m,
            Reason = reason,
            CreatedAt = now,
            ExpiresAt = request.ExpiresAt,
            CreatedByUserId = request.CreatedByUserId
        };
        dbContext.ManualRateOverrides.Add(rateOverride);
        await dbContext.SaveChangesAsync(cancellationToken);
        return rateOverride.Id;
    }
}

internal static class RateNormalizer
{
    public static string NormalizePair(string pair)
    {
        var normalized = pair.Replace("/", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToUpperInvariant();
        if (normalized.Length != 6 || normalized.Any(character => character is < 'A' or > 'Z'))
        {
            throw new FormatException($"The currency pair '{pair}' must contain two three-letter currency codes.");
        }

        return $"{normalized[..3]}/{normalized[3..]}";
    }
}
