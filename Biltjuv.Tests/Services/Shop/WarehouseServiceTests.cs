using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Biltjuv.Web.Infrastructure.Persistence.Entities;
using Biltjuv.Web.Infrastructure.Persistence.Repositories;
using Biltjuv.Web.Infrastructure.Warehouses;
using Biltjuv.Web.Services.Game;
using Biltjuv.Web.Services.Shop;

namespace Biltjuv.Tests.Services.Shop;

[TestFixture]
public sealed class WarehouseServiceTests
{
    private Mock<IGameDataRepository> _repository = null!;
    private Mock<IWarehouseCatalogService> _catalog = null!;
    private Mock<ILevelService> _levelService = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new Mock<IGameDataRepository>();
        _catalog = new Mock<IWarehouseCatalogService>();
        _levelService = new Mock<ILevelService>();
    }

    private WarehouseService CreateSut() => new(
        _repository.Object,
        _catalog.Object,
        _levelService.Object,
        NullLogger<WarehouseService>.Instance);

    private static UserGameDataEntity GameData(
        Guid userId, long money = 25000, int health = 100, int respect = 0, Guid? warehouseId = null) => new()
    {
        UserId = userId,
        Money = money,
        Health = health,
        Respect = respect,
        WarehouseId = warehouseId
    };

    private static WarehouseDefinition MakeWarehouse(
        Guid? id = null, long price = 25000, int minLevel = 1) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Name = "Koja",
        Space = 10,
        Price = price,
        MinLevel = minLevel,
        MaxSteal = 3,
        CreatedUtc = new DateTime(2021, 2, 27, 11, 24, 8, DateTimeKind.Utc)
    };

    [Test]
    public async Task PurchaseAsync_Should_Succeed_AndDeductMoney_AndSetOwnership()
    {
        var userId = Guid.NewGuid();
        var warehouse = MakeWarehouse(price: 25000, minLevel: 1);
        var data = GameData(userId, money: 25000);
        _repository.Setup(x => x.GetOrCreateAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(data);
        _catalog.Setup(x => x.GetByIdAsync(warehouse.Id, It.IsAny<CancellationToken>())).ReturnsAsync(warehouse);
        _levelService.Setup(x => x.GetLevel(It.IsAny<int>())).Returns(1);

        var result = await CreateSut().PurchaseAsync(userId, warehouse.Id);

        result.Outcome.Should().Be(WarehousePurchaseOutcome.Success);
        result.Warehouse.Should().Be(warehouse);
        data.Money.Should().Be(0);
        data.WarehouseId.Should().Be(warehouse.Id);
        _repository.Verify(x => x.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task PurchaseAsync_Should_Reject_WhenPlayerAlreadyOwnsAWarehouse()
    {
        var userId = Guid.NewGuid();
        var existingWarehouseId = Guid.NewGuid();
        var data = GameData(userId, warehouseId: existingWarehouseId);
        _repository.Setup(x => x.GetOrCreateAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(data);

        var result = await CreateSut().PurchaseAsync(userId, Guid.NewGuid());

        result.Outcome.Should().Be(WarehousePurchaseOutcome.AlreadyOwned);
        data.WarehouseId.Should().Be(existingWarehouseId, "the existing warehouse must not be replaced");
        _repository.Verify(x => x.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task PurchaseAsync_Should_Reject_WhenPlayerHasZeroHealth()
    {
        var userId = Guid.NewGuid();
        var data = GameData(userId, health: 0);
        _repository.Setup(x => x.GetOrCreateAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(data);

        var result = await CreateSut().PurchaseAsync(userId, Guid.NewGuid());

        result.Outcome.Should().Be(WarehousePurchaseOutcome.Dead);
        _catalog.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(x => x.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task PurchaseAsync_Should_Reject_WhenWarehouseIsNotInCatalog()
    {
        var userId = Guid.NewGuid();
        var data = GameData(userId);
        _repository.Setup(x => x.GetOrCreateAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(data);
        _catalog.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WarehouseDefinition?)null);

        var result = await CreateSut().PurchaseAsync(userId, Guid.NewGuid());

        result.Outcome.Should().Be(WarehousePurchaseOutcome.NotFound);
        _repository.Verify(x => x.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task PurchaseAsync_Should_Reject_WhenPlayerLevelIsBelowMinLevel()
    {
        var userId = Guid.NewGuid();
        var warehouse = MakeWarehouse(minLevel: 5);
        var data = GameData(userId);
        _repository.Setup(x => x.GetOrCreateAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(data);
        _catalog.Setup(x => x.GetByIdAsync(warehouse.Id, It.IsAny<CancellationToken>())).ReturnsAsync(warehouse);
        _levelService.Setup(x => x.GetLevel(It.IsAny<int>())).Returns(2);

        var result = await CreateSut().PurchaseAsync(userId, warehouse.Id);

        result.Outcome.Should().Be(WarehousePurchaseOutcome.LevelTooLow);
        result.RequiredLevel.Should().Be(5);
        result.CurrentLevel.Should().Be(2);
        _repository.Verify(x => x.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task PurchaseAsync_Should_Reject_WhenPlayerCannotAffordIt()
    {
        var userId = Guid.NewGuid();
        var warehouse = MakeWarehouse(price: 25000);
        var data = GameData(userId, money: 100);
        _repository.Setup(x => x.GetOrCreateAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(data);
        _catalog.Setup(x => x.GetByIdAsync(warehouse.Id, It.IsAny<CancellationToken>())).ReturnsAsync(warehouse);
        _levelService.Setup(x => x.GetLevel(It.IsAny<int>())).Returns(1);

        var result = await CreateSut().PurchaseAsync(userId, warehouse.Id);

        result.Outcome.Should().Be(WarehousePurchaseOutcome.InsufficientFunds);
        result.RequiredMoney.Should().Be(25000);
        result.CurrentMoney.Should().Be(100);
        data.Money.Should().Be(100, "a failed purchase must not touch the balance");
        _repository.Verify(x => x.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
