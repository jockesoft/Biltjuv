using Biltjuv.Web.Infrastructure.Persistence.Entities;

namespace Biltjuv.Web.Infrastructure.Persistence.Repositories;

public interface ILoginTokenRepository
{
    Task AddAsync(LoginTokenEntity token, CancellationToken cancellationToken = default);

    Task<LoginTokenEntity?> GetActiveByHashAsync(
        string tokenHash,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Atomically marks the token consumed; false if it was already consumed.</summary>
    Task<bool> MarkConsumedAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Marks every currently-unconsumed token for the user as consumed.</summary>
    Task InvalidateActiveForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
