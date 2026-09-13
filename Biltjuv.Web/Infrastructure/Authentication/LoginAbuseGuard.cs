using System.Collections.Concurrent;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace Biltjuv.Web.Infrastructure.Authentication;

/// <summary>
/// Two fixed-window limiters, both checked before a sign-in email is queued:
/// one keyed per email address (a day-long cap that survives many short
/// <see cref="LoginTokenOptions.ResendCooldownSeconds"/> windows), one shared
/// by everyone (an hourly cap that catches abuse spread across many
/// addresses or IPs). Registered as a singleton — state is per process, which
/// is fine for a single-instance deployment; it resets on restart.
/// </summary>
public sealed class LoginAbuseGuard : ILoginAbuseGuard, IDisposable
{
    private readonly RateLimiter _siteWideLimiter;
    private readonly PartitionedRateLimiter<string> _perEmailLimiter;

    // Per-user last-resend timestamp, checked and set atomically by
    // TryStartResendCooldown. Grows by one tiny entry per distinct user who has
    // ever requested a sign-in and is never pruned — fine at this scale, and
    // resets on restart along with everything else here.
    private readonly ConcurrentDictionary<Guid, DateTime> _lastResendUtc = new();

    public LoginAbuseGuard(IOptions<LoginTokenOptions> options)
    {
        var settings = options.Value;

        _siteWideLimiter = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Max(1, settings.MaxSignInEmailsPerHourSiteWide),
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });

        _perEmailLimiter = PartitionedRateLimiter.Create<string, string>(email =>
            RateLimitPartition.GetFixedWindowLimiter(email, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = Math.Max(1, settings.MaxRequestsPerEmailPerDay),
                Window = TimeSpan.FromDays(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    }

    public bool TryAcquire(string normalizedEmail)
    {
        // Cheapest, least specific check first.
        using var siteWideLease = _siteWideLimiter.AttemptAcquire();
        if (!siteWideLease.IsAcquired)
            return false;

        using var emailLease = _perEmailLimiter.AttemptAcquire(normalizedEmail);
        return emailLease.IsAcquired;
    }

    public bool TryStartResendCooldown(Guid userId, TimeSpan cooldown)
    {
        while (true)
        {
            var now = DateTime.UtcNow;

            if (!_lastResendUtc.TryGetValue(userId, out var last))
            {
                // No entry yet — try to be the first to claim this cooldown window.
                if (_lastResendUtc.TryAdd(userId, now))
                    return true;

                continue; // someone else added concurrently; retry and see their value
            }

            if (now - last < cooldown)
                return false; // still cooling down

            // Cooldown elapsed — try to replace the stale timestamp atomically.
            if (_lastResendUtc.TryUpdate(userId, now, last))
                return true;

            // Someone else updated it between our read and this attempt; retry.
        }
    }

    public void Dispose()
    {
        _siteWideLimiter.Dispose();
        _perEmailLimiter.Dispose();
    }
}
