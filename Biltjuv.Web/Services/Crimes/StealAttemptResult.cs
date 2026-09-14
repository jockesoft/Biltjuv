namespace Biltjuv.Web.Services.Crimes;

public enum StealAttemptOutcome
{
    /// <summary>The car was taken clean — money, respect, and a car added to the tally.</summary>
    Success,

    /// <summary>Got made mid-job — some health lost, nothing gained.</summary>
    Busted,

    /// <summary>Still cooling down from the last attempt; nothing happened.</summary>
    OnCooldown,

    /// <summary>No warehouse owned — nowhere to stash a stolen car.</summary>
    NoWarehouse,

    /// <summary>Warehouse is at capacity — no room for another car.</summary>
    WarehouseFull
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

    public static StealAttemptResult NoWarehouse() =>
        new(StealAttemptOutcome.NoWarehouse, 0, 0, 0, null);

    public static StealAttemptResult WarehouseFull() =>
        new(StealAttemptOutcome.WarehouseFull, 0, 0, 0, null);
}
