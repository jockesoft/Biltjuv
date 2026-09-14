using Biltjuv.Web.Infrastructure.Warehouses;

namespace Biltjuv.Web.Services.Shop;

public enum WarehousePurchaseOutcome
{
    /// <summary>Warehouse bought — money deducted, ownership recorded.</summary>
    Success,

    /// <summary>The player already owns a warehouse; no upgrade/second-warehouse path yet.</summary>
    AlreadyOwned,

    /// <summary>No warehouse with that id in the catalog.</summary>
    NotFound,

    /// <summary>Player is at 0 health — too beat up to make a deal.</summary>
    Dead,

    /// <summary>Player's level is below the warehouse's <see cref="WarehouseDefinition.MinLevel"/>.</summary>
    LevelTooLow,

    /// <summary>Player doesn't have enough money for the warehouse's <see cref="WarehouseDefinition.Price"/>.</summary>
    InsufficientFunds
}

public sealed record WarehousePurchaseResult(
    WarehousePurchaseOutcome Outcome,
    WarehouseDefinition? Warehouse = null,
    int RequiredLevel = 0,
    int CurrentLevel = 0,
    long RequiredMoney = 0,
    long CurrentMoney = 0)
{
    public static WarehousePurchaseResult Success(WarehouseDefinition warehouse) =>
        new(WarehousePurchaseOutcome.Success, warehouse);

    public static WarehousePurchaseResult AlreadyOwned() =>
        new(WarehousePurchaseOutcome.AlreadyOwned);

    public static WarehousePurchaseResult NotFound() =>
        new(WarehousePurchaseOutcome.NotFound);

    public static WarehousePurchaseResult Dead() =>
        new(WarehousePurchaseOutcome.Dead);

    public static WarehousePurchaseResult LevelTooLow(int requiredLevel, int currentLevel) =>
        new(WarehousePurchaseOutcome.LevelTooLow, RequiredLevel: requiredLevel, CurrentLevel: currentLevel);

    public static WarehousePurchaseResult InsufficientFunds(long requiredMoney, long currentMoney) =>
        new(WarehousePurchaseOutcome.InsufficientFunds, RequiredMoney: requiredMoney, CurrentMoney: currentMoney);
}
