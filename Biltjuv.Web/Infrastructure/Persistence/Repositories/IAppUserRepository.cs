using Biltjuv.Web.Infrastructure.Persistence.Entities;

namespace Biltjuv.Web.Infrastructure.Persistence.Repositories;

public interface IAppUserRepository
{
    Task<AppUserEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<AppUserEntity?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the existing account for <paramref name="email"/>, or provisions
    /// a new one (deriving a unique username from the email's local part).
    /// </summary>
    Task<AppUserEntity> GetOrCreateByEmailAsync(string email, CancellationToken cancellationToken = default);
}
