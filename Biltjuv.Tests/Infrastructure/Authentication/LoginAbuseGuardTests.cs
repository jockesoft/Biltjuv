using AwesomeAssertions;
using Microsoft.Extensions.Options;
using Biltjuv.Web.Infrastructure.Authentication;

namespace Biltjuv.Tests.Infrastructure.Authentication;

[TestFixture]
public sealed class LoginAbuseGuardTests
{
    private static LoginAbuseGuard CreateGuard(int maxPerEmailPerDay = 5, int maxSiteWidePerHour = 100)
    {
        var options = new LoginTokenOptions
        {
            MaxRequestsPerEmailPerDay = maxPerEmailPerDay,
            MaxSignInEmailsPerHourSiteWide = maxSiteWidePerHour
        };
        return new LoginAbuseGuard(Options.Create(options));
    }

    // ---- TryAcquire ---------------------------------------------------------

    [Test]
    public void TryAcquire_Should_AllowRequests_UpToPerEmailDailyLimit()
    {
        using var sut = CreateGuard(maxPerEmailPerDay: 3, maxSiteWidePerHour: 1000);

        sut.TryAcquire("user@test.local").Should().BeTrue();
        sut.TryAcquire("user@test.local").Should().BeTrue();
        sut.TryAcquire("user@test.local").Should().BeTrue();
    }

    [Test]
    public void TryAcquire_Should_Deny_OnceEmailExceedsDailyLimit()
    {
        using var sut = CreateGuard(maxPerEmailPerDay: 2, maxSiteWidePerHour: 1000);

        sut.TryAcquire("user@test.local").Should().BeTrue();
        sut.TryAcquire("user@test.local").Should().BeTrue();
        sut.TryAcquire("user@test.local").Should().BeFalse("the third request in the window exceeds the per-email cap");
    }

    [Test]
    public void TryAcquire_Should_TrackEachEmailAddressIndependently()
    {
        using var sut = CreateGuard(maxPerEmailPerDay: 1, maxSiteWidePerHour: 1000);

        sut.TryAcquire("a@test.local").Should().BeTrue();
        sut.TryAcquire("a@test.local").Should().BeFalse();
        sut.TryAcquire("b@test.local").Should().BeTrue("a different address has its own quota");
    }

    [Test]
    public void TryAcquire_Should_Deny_OnceSiteWideLimitExhausted_EvenForDifferentEmails()
    {
        using var sut = CreateGuard(maxPerEmailPerDay: 1000, maxSiteWidePerHour: 2);

        sut.TryAcquire("a@test.local").Should().BeTrue();
        sut.TryAcquire("b@test.local").Should().BeTrue();
        sut.TryAcquire("c@test.local").Should().BeFalse("the site-wide hourly cap has been reached");
    }

    // ---- TryStartResendCooldown ---------------------------------------------

    [Test]
    public void TryStartResendCooldown_Should_ReturnTrue_ForFirstRequest()
    {
        using var sut = CreateGuard();
        var userId = Guid.NewGuid();

        sut.TryStartResendCooldown(userId, TimeSpan.FromMinutes(2)).Should().BeTrue();
    }

    [Test]
    public void TryStartResendCooldown_Should_ReturnFalse_WhileCooldownStillActive()
    {
        using var sut = CreateGuard();
        var userId = Guid.NewGuid();

        sut.TryStartResendCooldown(userId, TimeSpan.FromMinutes(2)).Should().BeTrue();
        sut.TryStartResendCooldown(userId, TimeSpan.FromMinutes(2)).Should().BeFalse("a cooldown was just started and hasn't elapsed");
    }

    [Test]
    public void TryStartResendCooldown_Should_ReturnTrue_WhenCooldownAlreadyElapsed()
    {
        using var sut = CreateGuard();
        var userId = Guid.NewGuid();

        sut.TryStartResendCooldown(userId, TimeSpan.Zero).Should().BeTrue();
        sut.TryStartResendCooldown(userId, TimeSpan.Zero).Should().BeTrue("a zero-length cooldown has always already elapsed");
    }

    [Test]
    public void TryStartResendCooldown_Should_TrackEachUserIndependently()
    {
        using var sut = CreateGuard();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        sut.TryStartResendCooldown(userA, TimeSpan.FromMinutes(2)).Should().BeTrue();
        sut.TryStartResendCooldown(userB, TimeSpan.FromMinutes(2)).Should().BeTrue("a different user has their own cooldown");
    }
}
