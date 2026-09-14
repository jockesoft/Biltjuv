namespace Biltjuv.Web.Services.Game;

/// <summary>Converts a player's respect into their game level.</summary>
public interface ILevelService
{
    /// <summary>The level a player with this much respect has reached. Never below 1.</summary>
    int GetLevel(int respect);
}
