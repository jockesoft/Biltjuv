using Microsoft.EntityFrameworkCore;
using Biltjuv.Web.Infrastructure.Persistence.Entities;

namespace Biltjuv.Web.Infrastructure.Persistence.Repositories;

public sealed class LoginTokenRepository(AppDbContext dbContext) : ILoginTokenRepository
{
    public async Task AddAsync(LoginTokenEntity token, CancellationToken cancellationToken = default)
    {
        dbContext.LoginTokens.Add(token);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<LoginTokenEntity?> GetActiveByHashAsync(
        string tokenHash,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        return dbContext.LoginTokens
            .AsNoTracking()
            .Where(x => x.TokenHash == tokenHash
                        && x.ConsumedUtc == null
                        && x.ExpiresUtc > nowUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> MarkConsumedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var affected = await dbContext.LoginTokens
            .Where(x => x.Id == id && x.ConsumedUtc == null)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(x => x.ConsumedUtc, DateTime.UtcNow)
                    .SetProperty(x => x.UpdatedUtc, DateTime.UtcNow),
                cancellationToken);

        return affected > 0;
    }

    public async Task InvalidateActiveForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await dbContext.LoginTokens
            .Where(x => x.UserId == userId && x.ConsumedUtc == null)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(x => x.ConsumedUtc, DateTime.UtcNow)
                    .SetProperty(x => x.UpdatedUtc, DateTime.UtcNow),
                cancellationToken);
    }
}
