using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Biltjuv.Web.Services.Authentication;

namespace Biltjuv.Web.Pages.Account;

[AllowAnonymous]
[EnableRateLimiting("login-email")]
public sealed class LoginModel(
    IPasswordlessAuthService authService,
    ILogger<LoginModel> logger) : PageModel
{
    /// <summary>Real users take more than this to type an email and submit. Bots that
    /// clone-and-POST the form usually don't.</summary>
    private static readonly TimeSpan MinimumHumanSubmitDelay = TimeSpan.FromSeconds(2);

    [BindProperty]
    [Required]
    [EmailAddress]
    [StringLength(320)]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Honeypot. Hidden from users; automated form-fillers tend to populate any
    /// field named like this. A non-empty value means "bot" — we no-op.
    /// </summary>
    [BindProperty]
    public string? Website { get; set; }

    /// <summary>
    /// Timing trap: set to "now" when the form is rendered. A submission that
    /// arrives faster than a human could plausibly type an email address is
    /// treated the same as a tripped honeypot.
    /// </summary>
    [BindProperty]
    public string? FormRenderedAtUtc { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    /// <summary>True once a link has been sent, so the view shows the "check your inbox" state.</summary>
    public bool LinkSent { get; private set; }

    public void OnGet()
    {
        // Unix milliseconds rather than an ISO-8601 string: a plain integer has
        // no characters that need HTML-entity round-tripping through the hidden
        // input's rendered attribute value.
        FormRenderedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(Website) || !SubmittedLikeAHuman())
        {
            // Honeypot or timing trap tripped — look identical to a real
            // submission so the bot doesn't learn it was caught, but send
            // nothing.
            logger.LogWarning(
                "Login bot trap tripped from {RemoteIp} (honeypot: {Honeypot}).",
                HttpContext.Connection.RemoteIpAddress, !string.IsNullOrEmpty(Website));
            LinkSent = true;
            return Page();
        }

        if (!ModelState.IsValid)
            return Page();

        var scheme = Request.Scheme;
        var returnUrl = SafeReturnUrl();

        await authService.RequestLoginAsync(
            Email,
            token => Url.Page(
                "/Account/Verify",
                pageHandler: null,
                values: new { token, returnUrl },
                protocol: scheme)!,
            cancellationToken);

        // Always report the same thing whether or not the account existed.
        LinkSent = true;
        return Page();
    }

    private bool SubmittedLikeAHuman()
    {
        if (!long.TryParse(FormRenderedAtUtc, NumberStyles.Integer, CultureInfo.InvariantCulture, out var renderedAtMs))
            return false; // Missing or malformed — the field a real page render always sets.

        var renderedAt = DateTimeOffset.FromUnixTimeMilliseconds(renderedAtMs);
        return DateTimeOffset.UtcNow - renderedAt >= MinimumHumanSubmitDelay;
    }

    private string SafeReturnUrl() =>
        !string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : "/";
}
