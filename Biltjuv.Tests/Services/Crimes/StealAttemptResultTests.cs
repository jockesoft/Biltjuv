using AwesomeAssertions;
using Biltjuv.Web.Services.Crimes;

namespace Biltjuv.Tests.Services.Crimes;

[TestFixture]
public sealed class StealAttemptResultTests
{
    [Test]
    public void Cooldown_Should_SetOutcomeAndRemainingTime_WithNoRewardsOrLosses()
    {
        var remaining = TimeSpan.FromSeconds(12);

        var result = StealAttemptResult.Cooldown(remaining);

        result.Outcome.Should().Be(StealAttemptOutcome.OnCooldown);
        result.CooldownRemaining.Should().Be(remaining);
        result.MoneyGained.Should().Be(0);
        result.RespectGained.Should().Be(0);
        result.HealthLost.Should().Be(0);
    }

    [Test]
    public void Success_Should_SetOutcomeAndRewards_WithNoCooldownOrHealthLoss()
    {
        var result = StealAttemptResult.Success(120, 3);

        result.Outcome.Should().Be(StealAttemptOutcome.Success);
        result.MoneyGained.Should().Be(120);
        result.RespectGained.Should().Be(3);
        result.HealthLost.Should().Be(0);
        result.CooldownRemaining.Should().BeNull();
    }

    [Test]
    public void Busted_Should_SetOutcomeAndHealthLoss_WithNoRewards()
    {
        var result = StealAttemptResult.Busted(9);

        result.Outcome.Should().Be(StealAttemptOutcome.Busted);
        result.HealthLost.Should().Be(9);
        result.MoneyGained.Should().Be(0);
        result.RespectGained.Should().Be(0);
        result.CooldownRemaining.Should().BeNull();
    }

    [Test]
    public void NoWarehouse_Should_SetOutcome_WithNoRewardsLossesOrCooldown()
    {
        var result = StealAttemptResult.NoWarehouse();

        result.Outcome.Should().Be(StealAttemptOutcome.NoWarehouse);
        result.MoneyGained.Should().Be(0);
        result.RespectGained.Should().Be(0);
        result.HealthLost.Should().Be(0);
        result.CooldownRemaining.Should().BeNull();
    }

    [Test]
    public void WarehouseFull_Should_SetOutcome_WithNoRewardsLossesOrCooldown()
    {
        var result = StealAttemptResult.WarehouseFull();

        result.Outcome.Should().Be(StealAttemptOutcome.WarehouseFull);
        result.MoneyGained.Should().Be(0);
        result.RespectGained.Should().Be(0);
        result.HealthLost.Should().Be(0);
        result.CooldownRemaining.Should().BeNull();
    }
}
