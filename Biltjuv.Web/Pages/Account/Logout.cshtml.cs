using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Biltjuv.Web.Pages.Account;

public sealed class LogoutModel : PageModel
{
    // The nav bar links here with a plain GET, so support both verbs.
    public Task<IActionResult> OnGetAsync() => SignOutAndRedirectAsync();

    public Task<IActionResult> OnPostAsync() => SignOutAndRedirectAsync();

    private async Task<IActionResult> SignOutAndRedirectAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Index");
    }
}
