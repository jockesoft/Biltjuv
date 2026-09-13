using Microsoft.EntityFrameworkCore;
using Biltjuv.Web.Infrastructure.Persistence.Entities;

namespace Biltjuv.Web.Infrastructure.Persistence.Repositories;

public sealed class AppUserRepository(AppDbContext dbContext) : IAppUserRepository
{
    public Task<AppUserEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.AppUsers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<AppUserEntity?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return dbContext.AppUsers.FirstOrDefaultAsync(x => x.Email == normalized, cancellationToken);
    }

    public async Task<AppUserEntity> GetOrCreateByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();

        var existing = await dbContext.AppUsers
            .FirstOrDefaultAsync(x => x.Email == normalized, cancellationToken);

        if (existing is not null)
            return existing;

        var user = new AppUserEntity
        {
            Id = Guid.NewGuid(),
            Email = normalized,
            Username = await BuildUniqueUsernameAsync(normalized, cancellationToken)
        };

        dbContext.AppUsers.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return user;
    }

    /// <summary>
    /// Derives a display name from the email's local part, appending a short
    /// random suffix if that name is already taken (Username is unique).
    /// </summary>
    private async Task<string> BuildUniqueUsernameAsync(string email, CancellationToken cancellationToken)
    {
        var atIndex = email.IndexOf('@');
        var baseName = (atIndex > 0 ? email[..atIndex] : email).Trim();
        if (baseName.Length == 0)
            baseName = "user";
        if (baseName.Length > 180)
            baseName = baseName[..180];

        var candidate = baseName;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var taken = await dbContext.AppUsers
                .AnyAsync(x => x.Username == candidate, cancellationToken);

            if (!taken)
                return candidate;

            candidate = $"{baseName}-{Guid.NewGuid():N}"[..(baseName.Length + 9)];
        }

        return $"{baseName}-{Guid.NewGuid():N}";
    }
}
