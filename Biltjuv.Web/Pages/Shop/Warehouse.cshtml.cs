using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Biltjuv.Web.Infrastructure.Persistence.Entities;
using Biltjuv.Web.Infrastructure.Persistence.Repositories;
using Biltjuv.Web.Infrastructure.Warehouses;
using Biltjuv.Web.Services;
using Biltjuv.Web.Services.Game;
using Biltjuv.Web.Services.Shop;

namespace Biltjuv.Web.Pages.Shop;

[Authorize]
public sealed class WarehouseModel(
    IWarehouseService warehouseService,
    IWarehouseCatalogService catalogService,
    IGameDataRepository gameDataRepository,
    ILevelService levelService,
    ICurrentUserService currentUser) : PageModel
{
    public UserGameDataEntity GameData { get; private set; } = null!;

    public int Level { get; private set; }

    public IReadOnlyList<WarehouseShopRow> Warehouses { get; private set; } = [];

    public WarehousePurchaseResult? LastResult { get; private set; }

    public bool HasWarehouse => GameData.WarehouseId is not null;

    public bool IsDead => GameData.Health <= 0;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(Guid warehouseId, CancellationToken cancellationToken)
    {
        LastResult = await warehouseService.PurchaseAsync(currentUser.UserId!.Value, warehouseId, cancellationToken);
        await LoadAsync(cancellationToken);
        return Page();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        GameData = await gameDataRepository.GetOrCreateAsync(currentUser.UserId!.Value, cancellationToken);
        Level = levelService.GetLevel(GameData.Respect);

        var catalog = await catalogService.GetAllAsync(cancellationToken);
        Warehouses = catalog
            .OrderBy(w => w.Price)
            .Select(w => new WarehouseShopRow(
                w,
                IsOwned: GameData.WarehouseId == w.Id,
                MeetsLevel: Level >= w.MinLevel,
                CanAfford: GameData.Money >= w.Price))
            .ToList();
    }
}

public sealed record WarehouseShopRow(WarehouseDefinition Warehouse, bool IsOwned, bool MeetsLevel, bool CanAfford);
