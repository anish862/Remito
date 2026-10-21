using Remito.Domain.Entities;

namespace Remito.Infrastructure.Rates;

public sealed record RateQuote(
    string Pair,
    decimal Bid,
    decimal Ask,
    decimal Mid,
    Guid SourceId,
    DateTimeOffset FetchedAt);

public interface IRateProvider
{
    Guid SourceId { get; }
    string SourceName { get; }
    RateSourceType SourceType { get; }
    TimeSpan StalenessThreshold { get; }
    Task<IReadOnlyList<RateQuote>> FetchAsync(CancellationToken cancellationToken);
}

public sealed record ManualRateOverrideRequest(
    string Pair,
    decimal Bid,
    decimal Ask,
    string Reason,
    DateTimeOffset ExpiresAt,
    Guid? CreatedByUserId);

public interface IManualRateOverrideService
{
    Task<Guid> CreateAsync(ManualRateOverrideRequest request, CancellationToken cancellationToken);
}

public interface IRateIngestionJob
{
    Task RunAsync(CancellationToken cancellationToken);
}

public interface IRateHistoryRollupJob
{
    Task RunAsync(CancellationToken cancellationToken);
}
