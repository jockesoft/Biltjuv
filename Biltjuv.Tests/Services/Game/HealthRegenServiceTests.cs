using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Biltjuv.Web.Infrastructure.Game;
using Biltjuv.Web.Infrastructure.Persistence.Repositories;
using Biltjuv.Web.Services.Game;

namespace Biltjuv.Tests.Services.Game;

[TestFixture]
public sealed class HealthRegenServiceTests
{
    private Mock<IGameDataRepository> _repository = null!;
    private HealthRegenOptions _options = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new Mock<IGameDataRepository>();
        _options = new HealthRegenOptions { IntervalMinutes = 5, HealthPerTick = 10 };
    }

    private HealthRegenService CreateSut() => new(
        _repository.Object,
        Options.Create(_options),
        NullLogger<HealthRegenService>.Instance);

    [Test]
    public async Task RegenerateAsync_Should_CallRepository_WithConfiguredAmount_AndMaxHealthOf100()
    {
        _repository
            .Setup(x => x.RegenerateHealthAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        await CreateSut().RegenerateAsync();

        _repository.Verify(x => x.RegenerateHealthAsync(10, 100, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RegenerateAsync_Should_UseConfiguredHealthPerTick()
    {
        _options.HealthPerTick = 25;
        _repository
            .Setup(x => x.RegenerateHealthAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        await CreateSut().RegenerateAsync();

        _repository.Verify(x => x.RegenerateHealthAsync(25, 100, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RegenerateAsync_Should_NotThrow_WhenNoPlayersAffected()
    {
        _repository
            .Setup(x => x.RegenerateHealthAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var act = () => CreateSut().RegenerateAsync();

        await act.Should().NotThrowAsync();
    }
}
