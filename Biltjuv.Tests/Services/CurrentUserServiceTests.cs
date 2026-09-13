using System.Security.Claims;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Biltjuv.Web.Infrastructure.Authentication;
using Biltjuv.Web.Services;

namespace Biltjuv.Tests.Services;

[TestFixture]
public sealed class CurrentUserServiceTests
{
    private static CurrentUserService CreateSut(ClaimsPrincipal? user)
    {
        var httpContext = user is null ? null : new DefaultHttpContext { User = user };

        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(x => x.HttpContext).Returns(httpContext);

        return new CurrentUserService(accessor.Object);
    }

    private static ClaimsPrincipal AuthenticatedUser(Guid userId, string email, string displayName, string role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, displayName),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    [Test]
    public void IsAuthenticated_Should_BeFalse_WhenNoHttpContext()
    {
        var sut = CreateSut(null);

        sut.IsAuthenticated.Should().BeFalse();
    }

    [Test]
    public void IsAuthenticated_Should_BeFalse_ForAnonymousPrincipal()
    {
        var sut = CreateSut(new ClaimsPrincipal(new ClaimsIdentity()));

        sut.IsAuthenticated.Should().BeFalse();
    }

    [Test]
    public void IsAuthenticated_Should_BeTrue_ForAuthenticatedPrincipal()
    {
        var sut = CreateSut(AuthenticatedUser(Guid.NewGuid(), "user@test.local", "user", Roles.User));

        sut.IsAuthenticated.Should().BeTrue();
    }

    [Test]
    public void UserId_Should_ParseNameIdentifierClaim()
    {
        var userId = Guid.NewGuid();
        var sut = CreateSut(AuthenticatedUser(userId, "user@test.local", "user", Roles.User));

        sut.UserId.Should().Be(userId);
    }

    [Test]
    public void UserId_Should_BeNull_WhenNoHttpContext()
    {
        var sut = CreateSut(null);

        sut.UserId.Should().BeNull();
    }

    [Test]
    public void UserId_Should_BeNull_WhenNameIdentifierClaimMissing()
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Email, "user@test.local")], "TestAuth");
        var sut = CreateSut(new ClaimsPrincipal(identity));

        sut.UserId.Should().BeNull();
    }

    [Test]
    public void Email_And_DisplayName_And_Role_Should_ReadFromClaims()
    {
        var sut = CreateSut(AuthenticatedUser(Guid.NewGuid(), "user@test.local", "user", Roles.Admin));

        sut.Email.Should().Be("user@test.local");
        sut.DisplayName.Should().Be("user");
        sut.Role.Should().Be(Roles.Admin);
    }

    [Test]
    public void IsAdmin_Should_BeTrue_OnlyForAdminRole()
    {
        var admin = CreateSut(AuthenticatedUser(Guid.NewGuid(), "boss@test.local", "boss", Roles.Admin));
        var user = CreateSut(AuthenticatedUser(Guid.NewGuid(), "user@test.local", "user", Roles.User));

        admin.IsAdmin.Should().BeTrue();
        user.IsAdmin.Should().BeFalse();
    }

    [Test]
    public void IsInRole_Should_DelegateToClaimsPrincipal()
    {
        var sut = CreateSut(AuthenticatedUser(Guid.NewGuid(), "boss@test.local", "boss", Roles.Admin));

        sut.IsInRole(Roles.Admin).Should().BeTrue();
        sut.IsInRole(Roles.User).Should().BeFalse();
    }
}
