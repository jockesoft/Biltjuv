namespace Biltjuv.Web.Infrastructure.Warehouses;

/// <summary>
/// Read access to the warehouse catalog defined in the warehouses JSON config
/// file. Results are cached in Redis so the file is only re-read after the
/// cache entry expires (see <see cref="WarehouseOptions.CacheTtlHours"/>).
/// </summary>
public interface IWarehouseCatalogService
{
    Task<IReadOnlyList<WarehouseDefinition>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<WarehouseDefinition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
