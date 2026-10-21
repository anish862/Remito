using Remito.Application.Abstractions;

namespace Remito.Infrastructure.Caching;

public sealed class UnconfiguredRateCache : IRateCache
{
    public ValueTask<string?> GetAsync(string key, CancellationToken cancellationToken) =>
        ValueTask.FromException<string?>(
            new InvalidOperationException("Configure Redis before using the rate cache."));

    public ValueTask SetAsync(
        string key,
        string value,
        TimeSpan lifetime,
        CancellationToken cancellationToken) =>
        ValueTask.FromException(
            new InvalidOperationException("Configure Redis before using the rate cache."));
}
