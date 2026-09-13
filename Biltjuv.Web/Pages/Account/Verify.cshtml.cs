using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Biltjuv.Web.Services.Authentication;

namespace Biltjuv.Web.Pages.Account;

/// <summary>
/// Landing page for the link in the sign-in email: redeems the token and, on
/// success, issues the auth cookie and redirects on. Renders only in the
/// failure case.
/// </summary>
[AllowAnonymous]
public sealed class VerifyModel(IPasswordlessAuthService authService) : PageModel
{
    public async Task<IActionResult> OnGetAsync(
        string? token,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Page();

        var result = await authService.RedeemAsync(token, cancellationToken);
        if (!result.Succeeded)
            return Page();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.UserId.ToString()),
            new(ClaimTypes.Name, result.DisplayName),
            new(ClaimTypes.Email, result.Email),
            new(ClaimTypes.Role, result.Role.ToString())
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });

        var target = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl! : "/";
        return LocalRedirect(target);
    }
}
