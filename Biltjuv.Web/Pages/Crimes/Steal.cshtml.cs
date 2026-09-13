using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Biltjuv.Web.Infrastructure.Persistence.Entities;
using Biltjuv.Web.Services;
using Biltjuv.Web.Services.Crimes;

namespace Biltjuv.Web.Pages.Crimes;

[Authorize]
public sealed class StealModel(IStealService stealService, ICurrentUserService currentUser) : PageModel
{
    public UserGameDataEntity GameData { get; private set; } = null!;

    public StealAttemptResult? LastAttempt { get; private set; }

    public TimeSpan? CooldownRemaining { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        GameData = await stealService.GetGameDataAsync(currentUser.UserId!.Value, cancellationToken);
        ComputeCooldown();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        LastAttempt = await stealService.AttemptStealAsync(currentUser.UserId!.Value, cancellationToken);
        GameData = await stealService.GetGameDataAsync(currentUser.UserId!.Value, cancellationToken);
        ComputeCooldown();
        return Page();
    }

    private void ComputeCooldown()
    {
        var now = DateTime.UtcNow;
        CooldownRemaining = GameData.NextStealUtc is { } nextAt && nextAt > now
            ? nextAt - now
            : null;
    }
}
