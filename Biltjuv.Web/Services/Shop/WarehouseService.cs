using Biltjuv.Web.Infrastructure.Persistence.Repositories;
using Biltjuv.Web.Infrastructure.Warehouses;
using Biltjuv.Web.Services.Game;

namespace Biltjuv.Web.Services.Shop;

public sealed class WarehouseService(
    IGameDataRepository gameDataRepository,
    IWarehouseCatalogService catalogService,
    ILevelService levelService,
    ILogger<WarehouseService> logger) : IWarehouseService
{
    public async Task<WarehousePurchaseResult> PurchaseAsync(
        Guid userId, Guid warehouseId, CancellationToken cancellationToken = default)
    {
        var gameData = await gameDataRepository.GetOrCreateAsync(userId, cancellationToken);

        if (gameData.WarehouseId is not null)
            return WarehousePurchaseResult.AlreadyOwned();

        if (gameData.Health <= 0)
            return WarehousePurchaseResult.Dead();

        var warehouse = await catalogService.GetByIdAsync(warehouseId, cancellationToken);
        if (warehouse is null)
            return WarehousePurchaseResult.NotFound();

        var level = levelService.GetLevel(gameData.Respect);
        if (level < warehouse.MinLevel)
            return WarehousePurchaseResult.LevelTooLow(warehouse.MinLevel, level);

        if (gameData.Money < warehouse.Price)
            return WarehousePurchaseResult.InsufficientFunds(warehouse.Price, gameData.Money);

        gameData.Money -= warehouse.Price;
        gameData.WarehouseId = warehouse.Id;
        await gameDataRepository.SaveAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} purchased warehouse {WarehouseId} ({Name}) for {Price}.",
            userId, warehouse.Id, warehouse.Name, warehouse.Price);

        return WarehousePurchaseResult.Success(warehouse);
    }
}
