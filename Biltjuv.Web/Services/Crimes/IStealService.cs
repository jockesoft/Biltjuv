using Biltjuv.Web.Infrastructure.Persistence.Entities;

namespace Biltjuv.Web.Services.Crimes;

public interface IStealService
{
    /// <summary>Current stats for the player, creating a fresh row on first touch.</summary>
    Task<UserGameDataEntity> GetGameDataAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to steal a car. Does nothing but report <see cref="StealAttemptOutcome.OnCooldown"/>
    /// if the player's cooldown hasn't elapsed yet; otherwise rolls the outcome and updates their stats.
    /// </summary>
    Task<StealAttemptResult> AttemptStealAsync(Guid userId, CancellationToken cancellationToken = default);
}
