namespace Remito.Application.Abstractions;

public interface IRateCache
{
    ValueTask<string?> GetAsync(string key, CancellationToken cancellationToken);

    ValueTask SetAsync(string key, string value, TimeSpan lifetime, CancellationToken cancellationToken);
}
