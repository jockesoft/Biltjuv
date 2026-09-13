using System.Security.Cryptography;
using System.Text;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Biltjuv.Web.Infrastructure.Authentication;
using Biltjuv.Web.Infrastructure.Persistence.Entities;
using Biltjuv.Web.Infrastructure.Persistence.Repositories;
using Biltjuv.Web.Services;
using Biltjuv.Web.Services.Authentication;

namespace Biltjuv.Tests.Services.Authentication;

[TestFixture]
public sealed class PasswordlessAuthServiceTests
{
    private Mock<IAppUserRepository> _userRepository = null!;
    private Mock<ILoginTokenRepository> _tokenRepository = null!;
    private Mock<IMailService> _mailService = null!;
    private Mock<ILoginAbuseGuard> _abuseGuard = null!;
    private LoginTokenOptions _options = null!;
    private PasswordlessAuthService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _userRepository = new Mock<IAppUserRepository>();
        _tokenRepository = new Mock<ILoginTokenRepository>();
        _mailService = new Mock<IMailService>();
        _abuseGuard = new Mock<ILoginAbuseGuard>();
        _abuseGuard.Setup(x => x.TryAcquire(It.IsAny<string>())).Returns(true);
        _abuseGuard.Setup(x => x.TryStartResendCooldown(It.IsAny<Guid>(), It.IsAny<TimeSpan>())).Returns(true);
        _options = new LoginTokenOptions { TokenLifetimeMinutes = 15, InvalidatePreviousTokens = true };

        _sut = new PasswordlessAuthService(
            _userRepository.Object,
            _tokenRepository.Object,
            _mailService.Object,
            _abuseGuard.Object,
            Options.Create(_options),
            NullLogger<PasswordlessAuthService>.Instance);
    }

    private static string Sha256Base64(string value) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static AppUserEntity User(Guid id, string email, string username, UserRole role = UserRole.User) => new()
    {
        Id = id,
        Email = email,
        Username = username,
        Role = role
    };

    // ---- RequestLoginAsync ---------------------------------------------------

    [Test]
    public async Task RequestLoginAsync_Should_CreateHashedToken_AndQueueLinkEmail()
    {
        var user = User(Guid.NewGuid(), "user@test.local", "user");
        _userRepository
            .Setup(x => x.GetOrCreateByEmailAsync("user@test.local", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        LoginTokenEntity? storedToken = null;
        _tokenRepository
            .Setup(x => x.AddAsync(It.IsAny<LoginTokenEntity>(), It.IsAny<CancellationToken>()))
            .Callback<LoginTokenEntity, CancellationToken>((t, _) => storedToken = t)
            .Returns(Task.CompletedTask);

        string? queuedTo = null, queuedBody = null, queuedHtml = null;
        _mailService
            .Setup(x => x.QueueEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, string?, int, CancellationToken>((to, _, body, html, _, _) =>
            {
                queuedTo = to;
                queuedBody = body;
                queuedHtml = html;
            })
            .Returns(Task.CompletedTask);

        string? rawToken = null;

        await _sut.RequestLoginAsync(
            "user@test.local",
            token =>
            {
                rawToken = token;
                return $"https://biltjuv.test/Account/Verify?token={token}";
            });

        rawToken.Should().NotBeNullOrWhiteSpace();
        storedToken.Should().NotBeNull();
        storedToken!.UserId.Should().Be(user.Id);
        storedToken.TokenHash.Should().Be(Sha256Base64(rawToken!));
        storedToken.TokenHash.Should().NotBe(rawToken, "the raw token must never be stored");
        storedToken.ExpiresUtc.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(15), TimeSpan.FromMinutes(1));

        queuedTo.Should().Be(user.Email);
        queuedBody.Should().Contain($"https://biltjuv.test/Account/Verify?token={rawToken}");

        queuedHtml.Should().NotBeNullOrWhiteSpace();
        queuedHtml.Should().Contain($"https://biltjuv.test/Account/Verify?token={rawToken}");
        queuedHtml.Should().Contain("<a href=", "the HTML email links the token behind a button");
        queuedHtml.Should().Contain("Biltjuv");
    }

    [Test]
    public async Task RequestLoginAsync_Should_NormalizeEmailToLowercase()
    {
        _userRepository
            .Setup(x => x.GetOrCreateByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(User(Guid.NewGuid(), "mixed@test.local", "mixed"));

        await _sut.RequestLoginAsync("  Mixed@Test.LOCAL ", _ => "https://biltjuv.test/x");

        _userRepository.Verify(
            x => x.GetOrCreateByEmailAsync("mixed@test.local", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RequestLoginAsync_Should_InvalidatePreviousTokens_WhenOptionEnabled()
    {
        var user = User(Guid.NewGuid(), "user@test.local", "user");
        _userRepository
            .Setup(x => x.GetOrCreateByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        await _sut.RequestLoginAsync("user@test.local", _ => "https://biltjuv.test/x");

        _tokenRepository.Verify(
            x => x.InvalidateActiveForUserAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RequestLoginAsync_Should_NotInvalidatePreviousTokens_WhenOptionDisabled()
    {
        _options.InvalidatePreviousTokens = false;
        _userRepository
            .Setup(x => x.GetOrCreateByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(User(Guid.NewGuid(), "user@test.local", "user"));

        await _sut.RequestLoginAsync("user@test.local", _ => "https://biltjuv.test/x");

        _tokenRepository.Verify(
            x => x.InvalidateActiveForUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task RequestLoginAsync_Should_Reject_BlankEmail(string? email)
    {
        var act = () => _sut.RequestLoginAsync(email!, _ => "https://biltjuv.test/x");

        await act.Should().ThrowAsync<ArgumentException>();
        _tokenRepository.Verify(x => x.AddAsync(It.IsAny<LoginTokenEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task RequestLoginAsync_Should_SendNothing_WhenWithinResendCooldown()
    {
        _options.ResendCooldownSeconds = 120;
        var user = User(Guid.NewGuid(), "user@test.local", "user");
        _userRepository
            .Setup(x => x.GetOrCreateByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _abuseGuard
            .Setup(x => x.TryStartResendCooldown(user.Id, TimeSpan.FromSeconds(120)))
            .Returns(false);

        await _sut.RequestLoginAsync("user@test.local", _ => "https://biltjuv.test/x");

        _tokenRepository.Verify(x => x.InvalidateActiveForUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _tokenRepository.Verify(x => x.AddAsync(It.IsAny<LoginTokenEntity>(), It.IsAny<CancellationToken>()), Times.Never);
        _mailService.Verify(
            x => x.QueueEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task RequestLoginAsync_Should_SendEmail_WhenCooldownHasElapsed()
    {
        _options.ResendCooldownSeconds = 120;
        var user = User(Guid.NewGuid(), "user@test.local", "user");
        _userRepository
            .Setup(x => x.GetOrCreateByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _abuseGuard
            .Setup(x => x.TryStartResendCooldown(user.Id, TimeSpan.FromSeconds(120)))
            .Returns(true);

        await _sut.RequestLoginAsync("user@test.local", _ => "https://biltjuv.test/x");

        _tokenRepository.Verify(x => x.AddAsync(It.IsAny<LoginTokenEntity>(), It.IsAny<CancellationToken>()), Times.Once);
        _mailService.Verify(
            x => x.QueueEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task RequestLoginAsync_Should_IgnoreCooldown_WhenDisabled()
    {
        _options.ResendCooldownSeconds = 0;
        var user = User(Guid.NewGuid(), "user@test.local", "user");
        _userRepository
            .Setup(x => x.GetOrCreateByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        await _sut.RequestLoginAsync("user@test.local", _ => "https://biltjuv.test/x");

        _abuseGuard.Verify(
            x => x.TryStartResendCooldown(It.IsAny<Guid>(), It.IsAny<TimeSpan>()),
            Times.Never);
        _mailService.Verify(
            x => x.QueueEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task RequestLoginAsync_Should_DoNothing_WhenEmailNotOnAllowlist()
    {
        _options.AllowedEmails = ["allowed@test.local"];

        await _sut.RequestLoginAsync("intruder@test.local", _ => "https://biltjuv.test/x");

        _userRepository.Verify(
            x => x.GetOrCreateByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _tokenRepository.Verify(x => x.AddAsync(It.IsAny<LoginTokenEntity>(), It.IsAny<CancellationToken>()), Times.Never);
        _mailService.Verify(
            x => x.QueueEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task RequestLoginAsync_Should_SendLink_WhenEmailIsOnAllowlist_CaseInsensitively()
    {
        _options.AllowedEmails = ["allowed@test.local"];
        var user = User(Guid.NewGuid(), "allowed@test.local", "allowed");
        _userRepository
            .Setup(x => x.GetOrCreateByEmailAsync("allowed@test.local", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        await _sut.RequestLoginAsync("  Allowed@Test.LOCAL ", _ => "https://biltjuv.test/x");

        _mailService.Verify(
            x => x.QueueEmailAsync("allowed@test.local", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task RequestLoginAsync_Should_DoNothing_WhenAbuseGuardRejects()
    {
        _abuseGuard.Setup(x => x.TryAcquire("throttled@test.local")).Returns(false);

        await _sut.RequestLoginAsync("throttled@test.local", _ => "https://biltjuv.test/x");

        _userRepository.Verify(
            x => x.GetOrCreateByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _tokenRepository.Verify(x => x.AddAsync(It.IsAny<LoginTokenEntity>(), It.IsAny<CancellationToken>()), Times.Never);
        _mailService.Verify(
            x => x.QueueEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task RequestLoginAsync_Should_CheckAbuseGuard_WithNormalizedEmail()
    {
        _userRepository
            .Setup(x => x.GetOrCreateByEmailAsync("mixed@test.local", It.IsAny<CancellationToken>()))
            .ReturnsAsync(User(Guid.NewGuid(), "mixed@test.local", "mixed"));

        await _sut.RequestLoginAsync("  Mixed@Test.LOCAL ", _ => "https://biltjuv.test/x");

        _abuseGuard.Verify(x => x.TryAcquire("mixed@test.local"), Times.Once);
    }

    [Test]
    public async Task RequestLoginAsync_Should_AllowAnyEmail_WhenAllowlistEmpty()
    {
        _options.AllowedEmails = [];
        _userRepository
            .Setup(x => x.GetOrCreateByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(User(Guid.NewGuid(), "anyone@test.local", "anyone"));

        await _sut.RequestLoginAsync("anyone@test.local", _ => "https://biltjuv.test/x");

        _mailService.Verify(
            x => x.QueueEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ---- RedeemAsync -----------------------------------------------------

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task RedeemAsync_Should_ReturnInvalid_ForBlankToken(string? token)
    {
        var result = await _sut.RedeemAsync(token!);

        result.Succeeded.Should().BeFalse();
        _tokenRepository.Verify(
            x => x.GetActiveByHashAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task RedeemAsync_Should_ReturnInvalid_AndNotConsume_WhenTokenNotActive()
    {
        _tokenRepository
            .Setup(x => x.GetActiveByHashAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LoginTokenEntity?)null);

        var result = await _sut.RedeemAsync("some-raw-token");

        result.Status.Should().Be(LoginRedemptionStatus.InvalidOrExpired);
        _tokenRepository.Verify(x => x.MarkConsumedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task RedeemAsync_Should_LookUpByHash_ConsumeToken_AndReturnUser()
    {
        const string raw = "raw-token-value";
        var expectedHash = Sha256Base64(raw);

        var user = User(Guid.NewGuid(), "user@test.local", "user");
        var token = new LoginTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = expectedHash,
            ExpiresUtc = DateTime.UtcNow.AddMinutes(5)
        };

        _tokenRepository
            .Setup(x => x.GetActiveByHashAsync(expectedHash, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        _tokenRepository
            .Setup(x => x.MarkConsumedAsync(token.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _userRepository
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var result = await _sut.RedeemAsync(raw);

        result.Succeeded.Should().BeTrue();
        result.UserId.Should().Be(user.Id);
        result.Email.Should().Be("user@test.local");
        result.DisplayName.Should().Be("user");
        result.Role.Should().Be(UserRole.User);
        _tokenRepository.Verify(x => x.MarkConsumedAsync(token.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RedeemAsync_Should_ReturnInvalid_AndNotSignIn_WhenConsumeLosesRace()
    {
        // A concurrent redemption of the same token already consumed it between
        // our GetActiveByHashAsync read and this call's MarkConsumedAsync.
        const string raw = "raw-token-value";
        var expectedHash = Sha256Base64(raw);

        var token = new LoginTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TokenHash = expectedHash,
            ExpiresUtc = DateTime.UtcNow.AddMinutes(5)
        };

        _tokenRepository
            .Setup(x => x.GetActiveByHashAsync(expectedHash, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        _tokenRepository
            .Setup(x => x.MarkConsumedAsync(token.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.RedeemAsync(raw);

        result.Succeeded.Should().BeFalse();
        result.Status.Should().Be(LoginRedemptionStatus.InvalidOrExpired);
        _userRepository.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task RedeemAsync_Should_CarryThrough_AdminRole()
    {
        const string raw = "raw-token-value";
        var expectedHash = Sha256Base64(raw);

        var user = User(Guid.NewGuid(), "boss@test.local", "boss", UserRole.Admin);
        var token = new LoginTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = expectedHash,
            ExpiresUtc = DateTime.UtcNow.AddMinutes(5)
        };

        _tokenRepository
            .Setup(x => x.GetActiveByHashAsync(expectedHash, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        _tokenRepository
            .Setup(x => x.MarkConsumedAsync(token.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _userRepository
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var result = await _sut.RedeemAsync(raw);

        result.Succeeded.Should().BeTrue();
        result.Role.Should().Be(UserRole.Admin);
    }

    [Test]
    public async Task RedeemAsync_Should_ReturnInvalid_WhenUserNoLongerExists()
    {
        var token = new LoginTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TokenHash = Sha256Base64("raw"),
            ExpiresUtc = DateTime.UtcNow.AddMinutes(5)
        };
        _tokenRepository
            .Setup(x => x.GetActiveByHashAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        _tokenRepository
            .Setup(x => x.MarkConsumedAsync(token.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _userRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppUserEntity?)null);

        var result = await _sut.RedeemAsync("raw");

        result.Succeeded.Should().BeFalse();
    }
}
