using AwesomeAssertions;
using Biltjuv.Web.Infrastructure.Persistence.Entities;
using Biltjuv.Web.Services.Authentication;

namespace Biltjuv.Tests.Services.Authentication;

[TestFixture]
public sealed class LoginRedemptionResultTests
{
    [Test]
    public void Invalid_Should_HaveInvalidOrExpiredStatus_AndNotSucceed()
    {
        var result = LoginRedemptionResult.Invalid();

        result.Status.Should().Be(LoginRedemptionStatus.InvalidOrExpired);
        result.Succeeded.Should().BeFalse();
        result.UserId.Should().Be(Guid.Empty);
        result.Email.Should().BeEmpty();
        result.DisplayName.Should().BeEmpty();
    }

    [Test]
    public void ForUser_Should_HaveSuccessStatus_AndCarryGivenFields()
    {
        var userId = Guid.NewGuid();

        var result = LoginRedemptionResult.ForUser(userId, "user@test.local", "user", UserRole.Admin);

        result.Status.Should().Be(LoginRedemptionStatus.Success);
        result.Succeeded.Should().BeTrue();
        result.UserId.Should().Be(userId);
        result.Email.Should().Be("user@test.local");
        result.DisplayName.Should().Be("user");
        result.Role.Should().Be(UserRole.Admin);
    }
}
