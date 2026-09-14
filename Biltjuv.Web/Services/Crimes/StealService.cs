using Microsoft.Extensions.Options;
using Biltjuv.Web.Infrastructure.Crimes;
using Biltjuv.Web.Infrastructure.Persistence.Entities;
using Biltjuv.Web.Infrastructure.Persistence.Repositories;
using Biltjuv.Web.Infrastructure.Warehouses;

namespace Biltjuv.Web.Services.Crimes;

public sealed class StealService(
    IGameDataRepository gameDataRepository,
    IWarehouseCatalogService warehouseCatalogService,
    IOptions<StealOptions> options,
    ILogger<StealService> logger) : IStealService
{
    private readonly StealOptions _options = options.Value;

    public Task<UserGameDataEntity> GetGameDataAsync(Guid userId, CancellationToken cancellationToken = default) =>
        gameDataRepository.GetOrCreateAsync(userId, cancellationToken);

    public async Task<StealAttemptResult> AttemptStealAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var gameData = await gameDataRepository.GetOrCreateAsync(userId, cancellationToken);

        var now = DateTime.UtcNow;
        if (gameData.NextStealUtc is { } nextAt && nextAt > now)
            return StealAttemptResult.Cooldown(nextAt - now);

        // Stolen cars sit in the warehouse until fenced — no warehouse means nowhere
        // to stash one, and a full one means no room for another.
        if (gameData.WarehouseId is not { } warehouseId)
            return StealAttemptResult.NoWarehouse();

        var warehouse = await warehouseCatalogService.GetByIdAsync(warehouseId, cancellationToken);
        if (warehouse is null)
        {
            logger.LogWarning(
                "User {UserId} owns warehouse {WarehouseId}, which is not in the catalog; treating as no warehouse.",
                userId, warehouseId);
            return StealAttemptResult.NoWarehouse();
        }

        if (gameData.StolenCars >= warehouse.Space)
            return StealAttemptResult.WarehouseFull();

        gameData.NextStealUtc = now.AddSeconds(_options.CooldownSeconds);

        if (Random.Shared.Next(100) < _options.SuccessChancePercent)
        {
            var money = Random.Shared.Next(_options.MinMoneyReward, _options.MaxMoneyReward + 1);

            gameData.Money += money;
            gameData.Respect += _options.RespectReward;
            gameData.StolenCars += 1;

            await gameDataRepository.SaveAsync(cancellationToken);

            logger.LogInformation(
                "User {UserId} stole a car: +{Money} money, +{Respect} respect.",
                userId, money, _options.RespectReward);

            return StealAttemptResult.Success(money, _options.RespectReward);
        }
        else
        {
            var healthLost = Random.Shared.Next(_options.MinHealthLossOnBust, _options.MaxHealthLossOnBust + 1);
            gameData.Health = Math.Max(0, gameData.Health - healthLost);

            await gameDataRepository.SaveAsync(cancellationToken);

            logger.LogInformation("User {UserId} was busted stealing a car: -{HealthLost} health.", userId, healthLost);

            return StealAttemptResult.Busted(healthLost);
        }
    }
}
