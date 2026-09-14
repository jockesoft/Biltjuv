using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Biltjuv.Web.Infrastructure.Persistence;
using Biltjuv.Web.Infrastructure.Persistence.Entities;
using Biltjuv.Web.Infrastructure.Persistence.Repositories;

namespace Biltjuv.Tests.Infrastructure.Persistence.Repositories;

[TestFixture]
public sealed class GameDataRepositoryTests
{
    private SqliteConnection _connection = null!;
    private DbContextOptions<AppDbContext> _dbOptions = null!;

    [SetUp]
    public async Task SetUpAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using var dbContext = new AppDbContext(_dbOptions);
        await dbContext.Database.EnsureCreatedAsync();
    }

    [TearDown]
    public async Task TearDownAsync() => await _connection.DisposeAsync();

    private async Task SeedUserAsync(Guid userId)
    {
        await using var dbContext = new AppDbContext(_dbOptions);
        dbContext.AppUsers.Add(new AppUserEntity
        {
            Id = userId,
            Username = $"user-{userId:N}",
            Email = $"{userId:N}@test.local"
        });
        await dbContext.SaveChangesAsync();
    }

    [Test]
    public async Task GetOrCreateAsync_Should_CreateRow_WithStartingHealth_AndZeroedStats()
    {
        var userId = Guid.NewGuid();
        await SeedUserAsync(userId);

        await using var db = new AppDbContext(_dbOptions);
        var sut = new GameDataRepository(db);

        var data = await sut.GetOrCreateAsync(userId);

        data.UserId.Should().Be(userId);
        data.Health.Should().Be(100);
        data.Money.Should().Be(0);
        data.Respect.Should().Be(0);
        data.StolenCars.Should().Be(0);
        data.NextStealUtc.Should().BeNull();
    }

    [Test]
    public async Task GetOrCreateAsync_Should_ReturnSameRow_OnSubsequentCalls_WithoutDuplicating()
    {
        var userId = Guid.NewGuid();
        await SeedUserAsync(userId);

        await using var db = new AppDbContext(_dbOptions);
        var sut = new GameDataRepository(db);

        await sut.GetOrCreateAsync(userId);
        await sut.GetOrCreateAsync(userId);

        db.UserGameData.Count(x => x.UserId == userId).Should().Be(1);
    }

    [Test]
    public async Task GetOrCreateAsync_Should_ReturnPersistedChanges_MadeInAnEarlierScope()
    {
        var userId = Guid.NewGuid();
        await SeedUserAsync(userId);

        await using (var db = new AppDbContext(_dbOptions))
        {
            var sut = new GameDataRepository(db);
            var data = await sut.GetOrCreateAsync(userId);
            data.Money = 250;
            data.StolenCars = 3;
            await sut.SaveAsync();
        }

        await using (var db = new AppDbContext(_dbOptions))
        {
            var sut = new GameDataRepository(db);
            var data = await sut.GetOrCreateAsync(userId);

            data.Money.Should().Be(250);
            data.StolenCars.Should().Be(3);
        }
    }

    [Test]
    public async Task SaveAsync_Should_PersistMutationsMadeToATrackedEntity()
    {
        var userId = Guid.NewGuid();
        await SeedUserAsync(userId);

        await using var db = new AppDbContext(_dbOptions);
        var sut = new GameDataRepository(db);
        var data = await sut.GetOrCreateAsync(userId);

        data.Health = 42;
        await sut.SaveAsync();

        var reloaded = await db.UserGameData.AsNoTracking().SingleAsync(x => x.UserId == userId);
        reloaded.Health.Should().Be(42);
    }

    [Test]
    public async Task SaveAsync_Should_PersistWarehouseId()
    {
        var userId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        await SeedUserAsync(userId);

        await using (var db = new AppDbContext(_dbOptions))
        {
            var sut = new GameDataRepository(db);
            var data = await sut.GetOrCreateAsync(userId);
            data.WarehouseId.Should().BeNull("a player has no warehouse until they buy one");

            data.WarehouseId = warehouseId;
            await sut.SaveAsync();
        }

        await using (var db = new AppDbContext(_dbOptions))
        {
            var sut = new GameDataRepository(db);
            var data = await sut.GetOrCreateAsync(userId);

            data.WarehouseId.Should().Be(warehouseId);
        }
    }

    // ---- RegenerateHealthAsync ------------------------------------------------

    [Test]
    public async Task RegenerateHealthAsync_Should_RestoreHealth_ForPlayersBelowMax()
    {
        var userId = Guid.NewGuid();
        await SeedUserAsync(userId);
        await using var db = new AppDbContext(_dbOptions);
        var sut = new GameDataRepository(db);
        var data = await sut.GetOrCreateAsync(userId);
        data.Health = 50;
        await sut.SaveAsync();

        var affected = await sut.RegenerateHealthAsync(amount: 10, maxHealth: 100);

        affected.Should().Be(1);
        var reloaded = await db.UserGameData.AsNoTracking().SingleAsync(x => x.UserId == userId);
        reloaded.Health.Should().Be(60);
    }

    [Test]
    public async Task RegenerateHealthAsync_Should_CapAtMaxHealth_NotOvershoot()
    {
        var userId = Guid.NewGuid();
        await SeedUserAsync(userId);
        await using var db = new AppDbContext(_dbOptions);
        var sut = new GameDataRepository(db);
        var data = await sut.GetOrCreateAsync(userId);
        data.Health = 95;
        await sut.SaveAsync();

        await sut.RegenerateHealthAsync(amount: 10, maxHealth: 100);

        var reloaded = await db.UserGameData.AsNoTracking().SingleAsync(x => x.UserId == userId);
        reloaded.Health.Should().Be(100, "regen must never push health above the cap");
    }

    [Test]
    public async Task RegenerateHealthAsync_Should_SkipPlayers_AlreadyAtMaxHealth()
    {
        var userId = Guid.NewGuid();
        await SeedUserAsync(userId);
        await using var db = new AppDbContext(_dbOptions);
        var sut = new GameDataRepository(db);
        await sut.GetOrCreateAsync(userId); // starts at 100

        var affected = await sut.RegenerateHealthAsync(amount: 10, maxHealth: 100);

        affected.Should().Be(0);
    }

    [Test]
    public async Task RegenerateHealthAsync_Should_OnlyAffectPlayersBelowMax_AmongMany()
    {
        var full = Guid.NewGuid();
        var hurt = Guid.NewGuid();
        await SeedUserAsync(full);
        await SeedUserAsync(hurt);
        await using var db = new AppDbContext(_dbOptions);
        var sut = new GameDataRepository(db);
        await sut.GetOrCreateAsync(full); // starts at 100
        var hurtData = await sut.GetOrCreateAsync(hurt);
        hurtData.Health = 30;
        await sut.SaveAsync();

        var affected = await sut.RegenerateHealthAsync(amount: 10, maxHealth: 100);

        affected.Should().Be(1);
        (await db.UserGameData.AsNoTracking().SingleAsync(x => x.UserId == full)).Health.Should().Be(100);
        (await db.UserGameData.AsNoTracking().SingleAsync(x => x.UserId == hurt)).Health.Should().Be(40);
    }

    [Test]
    public async Task RegenerateHealthAsync_Should_BumpUpdatedUtc_ForAffectedPlayers()
    {
        var userId = Guid.NewGuid();
        await SeedUserAsync(userId);
        await using var db = new AppDbContext(_dbOptions);
        var sut = new GameDataRepository(db);
        var data = await sut.GetOrCreateAsync(userId);
        data.Health = 50;
        await sut.SaveAsync();
        var originalUpdatedUtc = data.UpdatedUtc;

        await Task.Delay(10);
        await sut.RegenerateHealthAsync(amount: 10, maxHealth: 100);

        var reloaded = await db.UserGameData.AsNoTracking().SingleAsync(x => x.UserId == userId);
        reloaded.UpdatedUtc.Should().BeAfter(originalUpdatedUtc);
    }
}
