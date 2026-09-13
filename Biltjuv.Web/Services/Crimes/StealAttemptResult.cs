namespace Biltjuv.Web.Services.Crimes;

public enum StealAttemptOutcome
{
    /// <summary>The car was taken clean — money, respect, and a car added to the tally.</summary>
    Success,

    /// <summary>Got made mid-job — some health lost, nothing gained.</summary>
    Busted,

    /// <summary>Still cooling down from the last attempt; nothing happened.</summary>
    OnCooldown
}

public sealed record StealAttemptResult(
    StealAttemptOutcome Outcome,
    long MoneyGained,
    int RespectGained,
    int HealthLost,
    TimeSpan? CooldownRemaining)
{
    public static StealAttemptResult Cooldown(TimeSpan remaining) =>
        new(StealAttemptOutcome.OnCooldown, 0, 0, 0, remaining);

    public static StealAttemptResult Success(long money, int respect) =>
        new(StealAttemptOutcome.Success, money, respect, 0, null);

    public static StealAttemptResult Busted(int healthLost) =>
        new(StealAttemptOutcome.Busted, 0, 0, healthLost, null);
}
