namespace Biltjuv.Web.Infrastructure.Crimes;

/// <summary>Bound from the <c>Steal</c> configuration section — tunes the car-theft crime.</summary>
public sealed class StealOptions
{
    public const string SectionName = "Steal";

    /// <summary>Minimum gap between attempts.</summary>
    public int CooldownSeconds { get; set; } = 30;

    /// <summary>Chance (0-100) that an attempt succeeds.</summary>
    public int SuccessChancePercent { get; set; } = 70;

    public int MinMoneyReward { get; set; } = 50;
    public int MaxMoneyReward { get; set; } = 150;
    public int RespectReward { get; set; } = 1;

    /// <summary>Health lost on a failed (busted) attempt.</summary>
    public int MinHealthLossOnBust { get; set; } = 5;
    public int MaxHealthLossOnBust { get; set; } = 15;
}
