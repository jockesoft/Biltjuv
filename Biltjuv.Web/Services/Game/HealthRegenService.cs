using Microsoft.Extensions.Options;
using Biltjuv.Web.Infrastructure.Game;
using Biltjuv.Web.Infrastructure.Persistence.Repositories;

namespace Biltjuv.Web.Services.Game;

public sealed class HealthRegenService(
    IGameDataRepository gameDataRepository,
    IOptions<HealthRegenOptions> options,
    ILogger<HealthRegenService> logger) : IHealthRegenService
{
    private const int MaxHealth = 100;

    private readonly HealthRegenOptions _options = options.Value;

    public async Task RegenerateAsync(CancellationToken cancellationToken = default)
    {
        var affected = await gameDataRepository.RegenerateHealthAsync(_options.HealthPerTick, MaxHealth, cancellationToken);

        if (affected > 0)
        {
            logger.LogInformation(
                "Health regen: restored {Amount} health to {Count} player(s).",
                _options.HealthPerTick, affected);
        }
    }
}
