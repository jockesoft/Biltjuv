using Quartz;
using Biltjuv.Web.Services.Game;

namespace Biltjuv.Web.Infrastructure.Timers;

/// <summary>
/// Restores health to every player below max. Scheduled in
/// <c>ServiceCollectionExtensions.AddScheduledJobs</c> at an interval read
/// from <c>HealthRegen:IntervalMinutes</c> (5 minutes in production, 2 in
/// Development via the appsettings override).
/// <see cref="DisallowConcurrentExecutionAttribute"/> keeps a slow run from
/// overlapping the next tick.
/// </summary>
[DisallowConcurrentExecution]
public sealed class HealthRegenTimer(IHealthRegenService healthRegenService, ILogger<HealthRegenTimer> logger) : IJob
{
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await healthRegenService.RegenerateAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Never let an unhandled exception escape into the scheduler.
            logger.LogError(ex, "HealthRegenTimer: unexpected failure while regenerating health.");
        }
    }
}
