using System.Text.Json;
using Microsoft.Extensions.Options;
using Biltjuv.Web.Infrastructure.Caching;

namespace Biltjuv.Web.Infrastructure.Warehouses;

public sealed class WarehouseCatalogService(
    IDistributedCacheJson cache,
    IOptions<WarehouseOptions> options,
    IHostEnvironment environment,
    ILogger<WarehouseCatalogService> logger) : IWarehouseCatalogService
{
    private const string CacheKey = "warehouses:catalog";

    private static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptions.Web;

    private readonly WarehouseOptions _options = options.Value;

    public async Task<IReadOnlyList<WarehouseDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var cached = await cache.GetAsync<List<WarehouseDefinition>>(CacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        var catalog = await LoadFromFileAsync(cancellationToken);

        await cache.SetAsync(CacheKey, catalog, TimeSpan.FromHours(_options.CacheTtlHours), cancellationToken);

        return catalog;
    }

    public async Task<WarehouseDefinition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var catalog = await GetAllAsync(cancellationToken);
        return catalog.FirstOrDefault(x => x.Id == id);
    }

    private async Task<List<WarehouseDefinition>> LoadFromFileAsync(CancellationToken cancellationToken)
    {
        var path = Path.Combine(environment.ContentRootPath, _options.FilePath);

        if (!File.Exists(path))
        {
            logger.LogWarning("Warehouse catalog file not found at {Path}; treating catalog as empty.", path);
            return [];
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var catalog = JsonSerializer.Deserialize<List<WarehouseDefinition>>(json, JsonOptions);

        return catalog ?? [];
    }
}
