using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Biltjuv.Web.Infrastructure.Crimes;
using Biltjuv.Web.Infrastructure.Persistence.Entities;
using Biltjuv.Web.Infrastructure.Persistence.Repositories;
using Biltjuv.Web.Services.Crimes;

namespace Biltjuv.Tests.Services.Crimes;

[TestFixture]
public sealed class StealServiceTests
{
    private Mock<IGameDataRepository> _repository = null!;
    private StealOptions _options = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new Mock<IGameDataRepository>();
        _options = new StealOptions
        {
            CooldownSeconds = 30,
            SuccessChancePercent = 100,
            MinMoneyReward = 100,
            MaxMoneyReward = 100,
            RespectReward = 2,
            MinHealthLossOnBust = 10,
            MaxHealthLossOnBust = 10
        };
    }

    private StealService CreateSut() => new(
        _repository.Object,
        Options.Create(_options),
        NullLogger<StealService>.Instance);

    private static UserGameDataEntity GameData(Guid userId, int health = 100, DateTime? nextStealUtc = null) => new()
    {
        UserId = userId,
        Health = health,
        Money = 0,
        Respect = 0,
        StolenCars = 0,
        NextStealUtc = nextStealUtc
    };

    [Test]
    public async Task GetGameDataAsync_Should_DelegateToRepository()
    {
        var userId = Guid.NewGuid();
        var data = GameData(userId);
        _repository.Setup(x => x.GetOrCreateAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(data);

        var result = await CreateSut().GetGameDataAsync(userId);

        result.Should().BeSameAs(data);
    }

    [Test]
    public async Task AttemptStealAsync_Should_ReturnOnCooldown_AndLeaveStatsUntouched_WhenCooldownActive()
    {
        var userId = Guid.NewGuid();
        var nextStealUtc = DateTime.UtcNow.AddSeconds(20);
        var data = GameData(userId, health: 100, nextStealUtc: nextStealUtc);
        _repository.Setup(x => x.GetOrCreateAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(data);

        var result = await CreateSut().AttemptStealAsync(userId);

        result.Outcome.Should().Be(StealAttemptOutcome.OnCooldown);
        result.CooldownRemaining.Should().NotBeNull();
        result.CooldownRemaining!.Value.Should().BeGreaterThan(TimeSpan.Zero);

        data.Money.Should().Be(0);
        data.Respect.Should().Be(0);
        data.StolenCars.Should().Be(0);
        data.Health.Should().Be(100);
        data.NextStealUtc.Should().Be(nextStealUtc, "an attempt made while on cooldown must not reset the timer");
        _repository.Verify(x => x.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task AttemptStealAsync_Should_AllowAttempt_WhenCooldownHasElapsed()
    {
        var userId = Guid.NewGuid();
        var data = GameData(userId, nextStealUtc: DateTime.UtcNow.AddSeconds(-1));
        _repository.Setup(x => x.GetOrCreateAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(data);

        var result = await CreateSut().AttemptStealAsync(userId);

        result.Outcome.Should().Be(StealAttemptOutcome.Success);
        _repository.Verify(x => x.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task AttemptStealAsync_Should_AwardMoneyRespectAndCar_OnSuccess()
    {
        var userId = Guid.NewGuid();
        var data = GameData(userId);
        _repository.Setup(x => x.GetOrCreateAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(data);

        var result = await CreateSut().AttemptStealAsync(userId);

        result.Outcome.Should().Be(StealAttemptOutcome.Success);
        result.MoneyGained.Should().Be(100);
        result.RespectGained.Should().Be(2);
        result.HealthLost.Should().Be(0);

        data.Money.Should().Be(100);
        data.Respect.Should().Be(2);
        data.StolenCars.Should().Be(1);
        data.Health.Should().Be(100, "a clean job costs no health");
        _repository.Verify(x => x.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task AttemptStealAsync_Should_SetCooldown_RelativeToConfiguredSeconds()
    {
        var userId = Guid.NewGuid();
        var data = GameData(userId);
        _repository.Setup(x => x.GetOrCreateAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(data);

        var before = DateTime.UtcNow;
        await CreateSut().AttemptStealAsync(userId);

        data.NextStealUtc.Should().NotBeNull();
        data.NextStealUtc!.Value.Should().BeCloseTo(before.AddSeconds(_options.CooldownSeconds), TimeSpan.FromSeconds(2));
    }

    [Test]
    public async Task AttemptStealAsync_Should_ReduceHealth_AndAwardNothing_OnBust()
    {
        _options.SuccessChancePercent = 0;
        var userId = Guid.NewGuid();
        var data = GameData(userId, health: 100);
        _repository.Setup(x => x.GetOrCreateAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(data);

        var result = await CreateSut().AttemptStealAsync(userId);

        result.Outcome.Should().Be(StealAttemptOutcome.Busted);
        result.HealthLost.Should().Be(10);
        result.MoneyGained.Should().Be(0);
        result.RespectGained.Should().Be(0);

        data.Health.Should().Be(90);
        data.Money.Should().Be(0);
        data.Respect.Should().Be(0);
        data.StolenCars.Should().Be(0);
        _repository.Verify(x => x.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task AttemptStealAsync_Should_FloorHealthAtZero_WhenBustWouldTakeItNegative()
    {
        _options.SuccessChancePercent = 0;
        var userId = Guid.NewGuid();
        var data = GameData(userId, health: 5);
        _repository.Setup(x => x.GetOrCreateAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(data);

        await CreateSut().AttemptStealAsync(userId);

        data.Health.Should().Be(0);
    }
}
