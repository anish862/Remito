using Remito.Application.Abstractions;
using StackExchange.Redis;

namespace Remito.Infrastructure.Caching;

public sealed class RedisRateCache(IConnectionMultiplexer connection) : IRateCache
{
    private readonly IDatabase database = connection.GetDatabase();

    public async ValueTask<string?> GetAsync(string key, CancellationToken cancellationToken)
    {
        var value = await database.StringGetAsync(key).WaitAsync(cancellationToken);
        return value.HasValue ? value.ToString() : null;
    }

    public async ValueTask SetAsync(
        string key,
        string value,
        TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        await database.StringSetAsync(key, value, lifetime).WaitAsync(cancellationToken);
    }
}
