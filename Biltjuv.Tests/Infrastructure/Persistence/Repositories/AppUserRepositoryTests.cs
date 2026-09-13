using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Biltjuv.Web.Infrastructure.Persistence;
using Biltjuv.Web.Infrastructure.Persistence.Entities;
using Biltjuv.Web.Infrastructure.Persistence.Repositories;

namespace Biltjuv.Tests.Infrastructure.Persistence.Repositories;

[TestFixture]
public sealed class AppUserRepositoryTests
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
    public async Task GetByIdAsync_Should_ReturnNull_WhenNoSuchUser()
    {
        await using var db = new AppDbContext(_dbOptions);
        var sut = new AppUserRepository(db);

        (await sut.GetByIdAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Test]
    public async Task GetByEmailAsync_Should_NormalizeEmail_BeforeMatching()
    {
        await using var db = new AppDbContext(_dbOptions);
        var sut = new AppUserRepository(db);
        await sut.GetOrCreateByEmailAsync("mixed@test.local");

        var found = await sut.GetByEmailAsync("  Mixed@Test.LOCAL ");

        found.Should().NotBeNull();
        found!.Email.Should().Be("mixed@test.local");
    }

    [Test]
    public async Task GetOrCreateByEmailAsync_Should_CreateNewUser_WithNormalizedEmail_AndDerivedUsername()
    {
        await using var db = new AppDbContext(_dbOptions);
        var sut = new AppUserRepository(db);

        var user = await sut.GetOrCreateByEmailAsync("  Jane.Doe@Test.LOCAL ");

        user.Id.Should().NotBe(Guid.Empty);
        user.Email.Should().Be("jane.doe@test.local");
        user.Username.Should().Be("jane.doe");
        user.Role.Should().Be(UserRole.User);
    }

    [Test]
    public async Task GetOrCreateByEmailAsync_Should_ReturnExistingUser_WithoutCreatingDuplicate()
    {
        await using var db = new AppDbContext(_dbOptions);
        var sut = new AppUserRepository(db);

        var first = await sut.GetOrCreateByEmailAsync("user@test.local");
        var second = await sut.GetOrCreateByEmailAsync("USER@TEST.LOCAL");

        second.Id.Should().Be(first.Id);
        db.AppUsers.Count().Should().Be(1);
    }

    [Test]
    public async Task GetOrCreateByEmailAsync_Should_AppendSuffix_WhenDerivedUsernameAlreadyTaken()
    {
        await using var db = new AppDbContext(_dbOptions);
        var sut = new AppUserRepository(db);
        await sut.GetOrCreateByEmailAsync("jane@one.local"); // claims username "jane"

        var second = await sut.GetOrCreateByEmailAsync("jane@two.local"); // also derives base name "jane"

        second.Username.Should().NotBe("jane");
        second.Username.Should().StartWith("jane-");
    }

    [Test]
    public async Task GetOrCreateByEmailAsync_Should_UseWholeAddress_WhenThereIsNoAtSign()
    {
        // The repository doesn't validate email format — it just derives a
        // username from whatever precedes '@', falling back to the whole
        // (normalized) string when there isn't one.
        await using var db = new AppDbContext(_dbOptions);
        var sut = new AppUserRepository(db);

        var user = await sut.GetOrCreateByEmailAsync("notanemail");

        user.Username.Should().Be("notanemail");
    }
}
