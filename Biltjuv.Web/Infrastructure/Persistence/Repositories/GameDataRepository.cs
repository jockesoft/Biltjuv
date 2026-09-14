using Microsoft.EntityFrameworkCore;
using Biltjuv.Web.Infrastructure.Persistence.Entities;

namespace Biltjuv.Web.Infrastructure.Persistence.Repositories;

public sealed class GameDataRepository(AppDbContext dbContext) : IGameDataRepository
{
    private const int StartingHealth = 100;

    // Covers the price of the cheapest warehouse (see Config/warehouses.json) so every
    // new player can buy storage for their first stolen car.
    private const long StartingMoney = 25000;

    public async Task<UserGameDataEntity> GetOrCreateAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.UserGameData
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        if (existing is not null)
            return existing;

        var gameData = new UserGameDataEntity
        {
            UserId = userId,
            Health = StartingHealth,
            Money = StartingMoney
        };

        dbContext.UserGameData.Add(gameData);
        await dbContext.SaveChangesAsync(cancellationToken);

        return gameData;
    }

    public Task SaveAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public Task<int> RegenerateHealthAsync(int amount, int maxHealth, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        return dbContext.UserGameData
            .Where(x => x.Health < maxHealth)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(x => x.Health, x => x.Health + amount > maxHealth ? maxHealth : x.Health + amount)
                    .SetProperty(x => x.UpdatedUtc, now),
                cancellationToken);
    }
}
