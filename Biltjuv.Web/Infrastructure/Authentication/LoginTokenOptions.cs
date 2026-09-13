namespace Biltjuv.Web.Infrastructure.Authentication;

/// <summary>
/// Bound from the <c>Login</c> configuration section — tunes the passwordless
/// (magic-link) sign-in flow.
/// </summary>
public sealed class LoginTokenOptions
{
    public const string SectionName = "Login";

    /// <summary>How long an emailed sign-in link stays valid.</summary>
    public int TokenLifetimeMinutes { get; set; } = 15;

    /// <summary>
    /// Minimum gap between sign-in emails to the same address. A repeat request
    /// inside this window is accepted but sends nothing, so the form can't be
    /// used to flood an inbox. Set to 0 to disable.
    /// </summary>
    public int ResendCooldownSeconds { get; set; } = 120;

    /// <summary>
    /// When true, requesting a new link consumes any earlier unused links for
    /// that account, so only the most recent email works.
    /// </summary>
    public bool InvalidatePreviousTokens { get; set; } = true;

    /// <summary>
    /// Email addresses permitted to request a sign-in link (and, on first use,
    /// have an account provisioned). Matched case-insensitively after trimming.
    /// When empty, any address is allowed.
    /// </summary>
    public string[] AllowedEmails { get; set; } = [];

    /// <summary>
    /// Extra cap alongside <see cref="ResendCooldownSeconds"/>: how many
    /// sign-in requests a single email address may make in a rolling 24 hours,
    /// even across cooldown windows. Enforced in-memory (per process).
    /// </summary>
    public int MaxRequestsPerEmailPerDay { get; set; } = 5;

    /// <summary>
    /// Site-wide circuit breaker: the most sign-in emails queued in a rolling
    /// hour across every requester combined. Enforced in-memory (per process).
    /// </summary>
    public int MaxSignInEmailsPerHourSiteWide { get; set; } = 100;
}
