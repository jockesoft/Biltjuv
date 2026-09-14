namespace Biltjuv.Web.Infrastructure.Game;

/// <summary>Bound from the <c>Level</c> configuration section — tunes how respect converts to player level.</summary>
public sealed class LevelOptions
{
    public const string SectionName = "Level";

    /// <summary>
    /// Respect needed per level beyond level 1 (linear curve): level 2 at
    /// <see cref="RespectPerLevel"/> respect, level 3 at double that, and so on.
    /// A simple starting point — the level formula can be swapped for a steeper/curved
    /// one later without touching callers.
    /// </summary>
    public int RespectPerLevel { get; set; } = 10;
}
