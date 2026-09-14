using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Biltjuv.Web.Infrastructure.Persistence.Entities;
using Biltjuv.Web.Infrastructure.Warehouses;
using Biltjuv.Web.Services;
using Biltjuv.Web.Services.Crimes;

namespace Biltjuv.Web.Pages.Crimes;

[Authorize]
public sealed class StealModel(
    IStealService stealService,
    IWarehouseCatalogService catalogService,
    ICurrentUserService currentUser) : PageModel
{
    public UserGameDataEntity GameData { get; private set; } = null!;

    /// <summary>The player's owned warehouse, if any — null means they haven't bought one yet.</summary>
    public WarehouseDefinition? Warehouse { get; private set; }

    public StealAttemptResult? LastAttempt { get; private set; }

    public TimeSpan? CooldownRemaining { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        LastAttempt = await stealService.AttemptStealAsync(currentUser.UserId!.Value, cancellationToken);
        await LoadAsync(cancellationToken);
        return Page();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        GameData = await stealService.GetGameDataAsync(currentUser.UserId!.Value, cancellationToken);
        Warehouse = GameData.WarehouseId is { } warehouseId
            ? await catalogService.GetByIdAsync(warehouseId, cancellationToken)
            : null;
        ComputeCooldown();
    }

    private void ComputeCooldown()
    {
        var now = DateTime.UtcNow;
        CooldownRemaining = GameData.NextStealUtc is { } nextAt && nextAt > now
            ? nextAt - now
            : null;
    }
}
