namespace Biltjuv.Web.Infrastructure.Game;

/// <summary>
/// Bound from the <c>HealthRegen</c> configuration section — tunes the
/// background timer that restores health to players below max over time.
/// </summary>
public sealed class HealthRegenOptions
{
    public const string SectionName = "HealthRegen";

    /// <summary>How often the regen timer ticks.</summary>
    public int IntervalMinutes { get; set; } = 5;

    /// <summary>How much health a tick restores to any player below max.</summary>
    public int HealthPerTick { get; set; } = 10;
}
