using Biltjuv.Web.Infrastructure.Persistence.Entities;

namespace Biltjuv.Web.Infrastructure.Persistence.Repositories;

public interface IGameDataRepository
{
    /// <summary>Returns the player's stats row, creating a fresh one (starting health, zero everything else) on first touch.</summary>
    Task<UserGameDataEntity> GetOrCreateAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Persists changes made to an entity returned by <see cref="GetOrCreateAsync"/>.</summary>
    Task SaveAsync(CancellationToken cancellationToken = default);
}
