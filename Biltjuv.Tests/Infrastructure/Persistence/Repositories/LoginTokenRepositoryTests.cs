using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Biltjuv.Web.Infrastructure.Persistence;
using Biltjuv.Web.Infrastructure.Persistence.Entities;
using Biltjuv.Web.Infrastructure.Persistence.Repositories;

namespace Biltjuv.Tests.Infrastructure.Persistence.Repositories;

[TestFixture]
public sealed class LoginTokenRepositoryTests
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

    [Test]
    public async Task GetActiveByHashAsync_Should_ReturnToken_WhenUnusedAndUnexpired()
    {
        var userId = Guid.NewGuid();
        await SeedUserAsync(userId);

        await using (var db = new AppDbContext(_dbOptions))
        {
            var sut = new LoginTokenRepository(db);
            await sut.AddAsync(new LoginTokenEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TokenHash = "hash-1",
                ExpiresUtc = DateTime.UtcNow.AddMinutes(10)
            });
        }

        await using (var db = new AppDbContext(_dbOptions))
        {
            var sut = new LoginTokenRepository(db);
            var found = await sut.GetActiveByHashAsync("hash-1", DateTime.UtcNow);

            found.Should().NotBeNull();
            found!.UserId.Should().Be(userId);
        }
    }

    [Test]
    public async Task GetActiveByHashAsync_Should_ReturnNull_WhenExpired()
    {
        var userId = Guid.NewGuid();
        await SeedUserAsync(userId);

        await using var db = new AppDbContext(_dbOptions);
        var sut = new LoginTokenRepository(db);
        await sut.AddAsync(new LoginTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = "hash-expired",
            ExpiresUtc = DateTime.UtcNow.AddMinutes(-1)
        });

        (await sut.GetActiveByHashAsync("hash-expired", DateTime.UtcNow)).Should().BeNull();
    }

    [Test]
    public async Task MarkConsumedAsync_Should_MakeTokenInactive_AndReturnTrue()
    {
        var userId = Guid.NewGuid();
        await SeedUserAsync(userId);

        var tokenId = Guid.NewGuid();
        await using var db = new AppDbContext(_dbOptions);
        var sut = new LoginTokenRepository(db);
        await sut.AddAsync(new LoginTokenEntity
        {
            Id = tokenId,
            UserId = userId,
            TokenHash = "hash-2",
            ExpiresUtc = DateTime.UtcNow.AddMinutes(10)
        });

        (await sut.MarkConsumedAsync(tokenId)).Should().BeTrue();
        (await sut.GetActiveByHashAsync("hash-2", DateTime.UtcNow)).Should().BeNull();
    }

    [Test]
    public async Task MarkConsumedAsync_Should_ReturnFalse_WhenAlreadyConsumed()
    {
        var userId = Guid.NewGuid();
        await SeedUserAsync(userId);

        var tokenId = Guid.NewGuid();
        await using var db = new AppDbContext(_dbOptions);
        var sut = new LoginTokenRepository(db);
        await sut.AddAsync(new LoginTokenEntity
        {
            Id = tokenId,
            UserId = userId,
            TokenHash = "hash-3",
            ExpiresUtc = DateTime.UtcNow.AddMinutes(10)
        });

        (await sut.MarkConsumedAsync(tokenId)).Should().BeTrue("the first consume wins");
        (await sut.MarkConsumedAsync(tokenId)).Should().BeFalse("a second consume of the same token must not also succeed");
    }

    [Test]
    public async Task MarkConsumedAsync_Should_ReturnFalse_WhenTokenDoesNotExist()
    {
        await using var db = new AppDbContext(_dbOptions);
        var sut = new LoginTokenRepository(db);

        (await sut.MarkConsumedAsync(Guid.NewGuid())).Should().BeFalse();
    }

    [Test]
    public async Task InvalidateActiveForUserAsync_Should_ConsumeEveryUnusedTokenForThatUser()
    {
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        await SeedUserAsync(userId);
        await SeedUserAsync(otherUserId);

        await using var db = new AppDbContext(_dbOptions);
        var sut = new LoginTokenRepository(db);
        await sut.AddAsync(new LoginTokenEntity { Id = Guid.NewGuid(), UserId = userId, TokenHash = "a", ExpiresUtc = DateTime.UtcNow.AddMinutes(10) });
        await sut.AddAsync(new LoginTokenEntity { Id = Guid.NewGuid(), UserId = userId, TokenHash = "b", ExpiresUtc = DateTime.UtcNow.AddMinutes(10) });
        await sut.AddAsync(new LoginTokenEntity { Id = Guid.NewGuid(), UserId = otherUserId, TokenHash = "c", ExpiresUtc = DateTime.UtcNow.AddMinutes(10) });

        await sut.InvalidateActiveForUserAsync(userId);

        (await sut.GetActiveByHashAsync("a", DateTime.UtcNow)).Should().BeNull();
        (await sut.GetActiveByHashAsync("b", DateTime.UtcNow)).Should().BeNull();
        (await sut.GetActiveByHashAsync("c", DateTime.UtcNow)).Should().NotBeNull("another user's token is untouched");
    }

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
}
