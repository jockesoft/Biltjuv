namespace Biltjuv.Web.Services.Game;

/// <summary>Restores health to players below max, driven by the <c>HealthRegenTimer</c> Quartz job.</summary>
public interface IHealthRegenService
{
    Task RegenerateAsync(CancellationToken cancellationToken = default);
}
