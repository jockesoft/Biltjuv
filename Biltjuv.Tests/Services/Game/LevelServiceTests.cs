using AwesomeAssertions;
using Microsoft.Extensions.Options;
using Biltjuv.Web.Infrastructure.Game;
using Biltjuv.Web.Services.Game;

namespace Biltjuv.Tests.Services.Game;

[TestFixture]
public sealed class LevelServiceTests
{
    private LevelService CreateSut(int respectPerLevel = 10) =>
        new(Options.Create(new LevelOptions { RespectPerLevel = respectPerLevel }));

    [TestCase(0, 1)]
    [TestCase(-5, 1)]
    [TestCase(1, 1)]
    [TestCase(9, 1)]
    [TestCase(10, 2)]
    [TestCase(19, 2)]
    [TestCase(20, 3)]
    [TestCase(100, 11)]
    public void GetLevel_Should_FollowConfiguredLinearCurve(int respect, int expectedLevel)
    {
        CreateSut().GetLevel(respect).Should().Be(expectedLevel);
    }

    [Test]
    public void GetLevel_Should_NeverReturnBelowOne()
    {
        CreateSut().GetLevel(int.MinValue).Should().Be(1);
    }

    [Test]
    public void GetLevel_Should_UseConfiguredRespectPerLevel()
    {
        var sut = CreateSut(respectPerLevel: 5);

        sut.GetLevel(5).Should().Be(2);
        sut.GetLevel(10).Should().Be(3);
    }
}
