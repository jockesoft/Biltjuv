using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Biltjuv.Web.Infrastructure.Persistence;
using Biltjuv.Web.Infrastructure.Persistence.Entities;
using Biltjuv.Web.Infrastructure.Persistence.Repositories;

namespace Biltjuv.Tests.Infrastructure.Persistence.Repositories;

[TestFixture]
public sealed class EmailRepositoryTests
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

    private static EmailEntity Email(string to, int priority = 0, int sendAttempts = 0, DateTime? sentUtc = null) => new()
    {
        Id = Guid.NewGuid(),
        Priority = priority,
        ToAddress = to,
        Subject = "Subject",
        Body = "Body",
        SendAttempts = sendAttempts,
        SentUtc = sentUtc
    };

    [Test]
    public async Task AddAsync_Should_PersistEmail_Retrievable_AsPending()
    {
        await using var db = new AppDbContext(_dbOptions);
        var sut = new EmailRepository(db);

        await sut.AddAsync(Email("user@test.local"));

        var pending = await sut.GetPendingAsync(maxCount: 10, maxAttempts: 5);
        pending.Should().ContainSingle(x => x.ToAddress == "user@test.local");
    }

    [Test]
    public async Task GetPendingAsync_Should_ExcludeAlreadySentEmails()
    {
        await using var db = new AppDbContext(_dbOptions);
        var sut = new EmailRepository(db);
        await sut.AddAsync(Email("sent@test.local", sentUtc: DateTime.UtcNow));
        await sut.AddAsync(Email("pending@test.local"));

        var pending = await sut.GetPendingAsync(maxCount: 10, maxAttempts: 5);

        pending.Should().ContainSingle();
        pending[0].ToAddress.Should().Be("pending@test.local");
    }

    [Test]
    public async Task GetPendingAsync_Should_ExcludeEmails_AtOrAboveMaxAttempts()
    {
        await using var db = new AppDbContext(_dbOptions);
        var sut = new EmailRepository(db);
        await sut.AddAsync(Email("exhausted@test.local", sendAttempts: 5));
        await sut.AddAsync(Email("retryable@test.local", sendAttempts: 4));

        var pending = await sut.GetPendingAsync(maxCount: 10, maxAttempts: 5);

        pending.Should().ContainSingle();
        pending[0].ToAddress.Should().Be("retryable@test.local");
    }

    [Test]
    public async Task GetPendingAsync_Should_OrderByPriority_ThenCreatedUtc()
    {
        await using var db = new AppDbContext(_dbOptions);
        var sut = new EmailRepository(db);

        await sut.AddAsync(Email("second@test.local", priority: 0));
        await sut.AddAsync(Email("first@test.local", priority: -5));
        await sut.AddAsync(Email("third@test.local", priority: 10));

        var pending = await sut.GetPendingAsync(maxCount: 10, maxAttempts: 5);

        pending.Select(x => x.ToAddress).Should().ContainInOrder("first@test.local", "second@test.local", "third@test.local");
    }

    [Test]
    public async Task GetPendingAsync_Should_RespectMaxCount()
    {
        await using var db = new AppDbContext(_dbOptions);
        var sut = new EmailRepository(db);
        for (var i = 0; i < 5; i++)
            await sut.AddAsync(Email($"user{i}@test.local"));

        var pending = await sut.GetPendingAsync(maxCount: 2, maxAttempts: 5);

        pending.Should().HaveCount(2);
    }

    [Test]
    public async Task MarkSentAsync_Should_SetSentUtc_AndRemoveFromPending()
    {
        var email = Email("user@test.local");
        await using var db = new AppDbContext(_dbOptions);
        var sut = new EmailRepository(db);
        await sut.AddAsync(email);

        await sut.MarkSentAsync(email.Id);

        (await sut.GetPendingAsync(10, 5)).Should().BeEmpty();
    }

    [Test]
    public async Task RecordFailedAttemptAsync_Should_IncrementSendAttempts()
    {
        var email = Email("user@test.local");
        await using var db = new AppDbContext(_dbOptions);
        var sut = new EmailRepository(db);
        await sut.AddAsync(email);

        await sut.RecordFailedAttemptAsync(email.Id);
        await sut.RecordFailedAttemptAsync(email.Id);

        var pending = await sut.GetPendingAsync(10, 5);
        pending.Should().ContainSingle(x => x.Id == email.Id && x.SendAttempts == 2);
    }
}
