using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Biltjuv.Web.Infrastructure.Crimes;
using Biltjuv.Web.Infrastructure.Persistence.Entities;
using Biltjuv.Web.Infrastructure.Persistence.Repositories;
using Biltjuv.Web.Infrastructure.Warehouses;
using Biltjuv.Web.Services.Crimes;

namespace Biltjuv.Tests.Services.Crimes;

[TestFixture]
public sealed class StealServiceTests
{
    // Ample space so tests that aren't specifically about warehouse capacity
    // never trip the full-warehouse check.
    private static readonly Guid DefaultWarehouseId = Guid.NewGuid();
    private static readonly WarehouseDefinition DefaultWarehouse = new()
    {
        Id = DefaultWarehouseId,
        Name = "Koja",
        Space = 100,
        Price = 25000,
        MinLevel = 1,
        MaxSteal = 3,
        CreatedUtc = new DateTime(2021, 2, 27, 11, 24, 8, DateTimeKind.Utc)
    };

    private Mock<IGameDataRepository> _repository = null!;
    private Mock<IWarehouseCatalogService> _catalog = null!;
    private StealOptions _options = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new Mock<IGameDataRepository>();
        _catalog = new Mock<IWarehouseCatalogService>();
        _catalog.Setup(x => x.GetByIdAsync(DefaultWarehouseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultWarehouse);
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
        _catalog.Object,
        Options.Create(_options),
        NullLogger<StealService>.Instance);

    private static UserGameDataEntity GameData(
        Guid userId, int health = 100, DateTime? nextStealUtc = null, int stolenCars = 0, Guid? warehouseId = null) => new()
    {
        UserId = userId,
        Health = health,
        Money = 0,
        Respect = 0,
        StolenCars = stolenCars,
        NextStealUtc = nextStealUtc,
        WarehouseId = warehouseId ?? DefaultWarehouseId
    };

    private static WarehouseDefinition MakeWarehouse(int space) => new()
    {
        Id = DefaultWarehouseId,
        Name = DefaultWarehouse.Name,
        Space = space,
        Price = DefaultWarehouse.Price,
        MinLevel = DefaultWarehouse.MinLevel,
        MaxSteal = DefaultWarehouse.MaxSteal,
        CreatedUtc = DefaultWarehouse.CreatedUtc
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

    // ---- Warehouse gating ---------------------------------------------------

    [Test]
    public async Task AttemptStealAsync_Should_ReturnNoWarehouse_WhenPlayerOwnsNone()
    {
        var userId = Guid.NewGuid();
        var data = GameData(userId);
        data.WarehouseId = null;
        _repository.Setup(x => x.GetOrCreateAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(data);

        var result = await CreateSut().AttemptStealAsync(userId);

        result.Outcome.Should().Be(StealAttemptOutcome.NoWarehouse);
        data.NextStealUtc.Should().BeNull("a rejected attempt must not start a cooldown");
        _repository.Verify(x => x.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task AttemptStealAsync_Should_ReturnNoWarehouse_WhenOwnedWarehouseIsNotInCatalog()
    {
        var userId = Guid.NewGuid();
        var unknownWarehouseId = Guid.NewGuid();
        var data = GameData(userId, warehouseId: unknownWarehouseId);
        _repository.Setup(x => x.GetOrCreateAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(data);
        _catalog.Setup(x => x.GetByIdAsync(unknownWarehouseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WarehouseDefinition?)null);

        var result = await CreateSut().AttemptStealAsync(userId);

        result.Outcome.Should().Be(StealAttemptOutcome.NoWarehouse);
        _repository.Verify(x => x.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task AttemptStealAsync_Should_ReturnWarehouseFull_WhenStolenCarsReachesSpace()
    {
        var userId = Guid.NewGuid();
        var warehouse = MakeWarehouse(space: 3);
        var data = GameData(userId, stolenCars: 3);
        _repository.Setup(x => x.GetOrCreateAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(data);
        _catalog.Setup(x => x.GetByIdAsync(DefaultWarehouseId, It.IsAny<CancellationToken>())).ReturnsAsync(warehouse);

        var result = await CreateSut().AttemptStealAsync(userId);

        result.Outcome.Should().Be(StealAttemptOutcome.WarehouseFull);
        data.StolenCars.Should().Be(3, "a rejected attempt must not change stats");
        data.NextStealUtc.Should().BeNull("a rejected attempt must not start a cooldown");
        _repository.Verify(x => x.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task AttemptStealAsync_Should_Allow_WhenWarehouseHasExactlyOneSpaceLeft()
    {
        var userId = Guid.NewGuid();
        var warehouse = MakeWarehouse(space: 3);
        var data = GameData(userId, stolenCars: 2);
        _repository.Setup(x => x.GetOrCreateAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(data);
        _catalog.Setup(x => x.GetByIdAsync(DefaultWarehouseId, It.IsAny<CancellationToken>())).ReturnsAsync(warehouse);

        var result = await CreateSut().AttemptStealAsync(userId);

        result.Outcome.Should().Be(StealAttemptOutcome.Success);
        data.StolenCars.Should().Be(3);
        _repository.Verify(x => x.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
