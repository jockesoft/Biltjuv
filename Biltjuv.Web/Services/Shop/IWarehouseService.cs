namespace Biltjuv.Web.Services.Shop;

/// <summary>Handles a player buying a warehouse from the catalog.</summary>
public interface IWarehouseService
{
    Task<WarehousePurchaseResult> PurchaseAsync(Guid userId, Guid warehouseId, CancellationToken cancellationToken = default);
}
