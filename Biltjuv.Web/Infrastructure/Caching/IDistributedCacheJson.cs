namespace Biltjuv.Web.Infrastructure.Caching;

/// <summary>Thin JSON wrapper over <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/> (Redis).</summary>
public interface IDistributedCacheJson
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}
