using System.Collections.Concurrent;
using Remito.Application.Abstractions;

namespace Remito.Infrastructure.Caching;

public sealed class DevelopmentRateCache(TimeProvider timeProvider) : IRateCache
{
    private readonly ConcurrentDictionary<string, CacheEntry> entries = new(StringComparer.Ordinal);

    public ValueTask<string?> GetAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!entries.TryGetValue(key, out var entry))
        {
            return ValueTask.FromResult<string?>(null);
        }

        if (entry.ExpiresAt <= timeProvider.GetUtcNow())
        {
            entries.TryRemove(key, out _);
            return ValueTask.FromResult<string?>(null);
        }

        return ValueTask.FromResult<string?>(entry.Value);
    }

    public ValueTask SetAsync(
        string key,
        string value,
        TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        entries[key] = new CacheEntry(value, timeProvider.GetUtcNow().Add(lifetime));
        return ValueTask.CompletedTask;
    }

    private sealed record CacheEntry(string Value, DateTimeOffset ExpiresAt);
}
