using System.Security.Claims;
using Biltjuv.Web.Infrastructure.Persistence.Entities;

namespace Biltjuv.Web.Middleware;

#if DEBUG
public class DevAuthMiddleware(RequestDelegate next, IHostEnvironment environment, ILogger<DevAuthMiddleware> logger)
#else
// Release builds never reference logger (the whole DEBUG block below is
// stripped), so it's dropped here rather than left as an unread
// primary-constructor parameter. UseMiddleware<T> resolves whichever
// constructor exists via DI, so this is a transparent swap either way.
public class DevAuthMiddleware(RequestDelegate next)
#endif
{
    private static readonly Guid FixedDevUserId = Guid.Parse("00c74b65-53ac-4c58-89e8-8e4c36e58a0d");

    public async Task InvokeAsync(HttpContext context)
    {
#if DEBUG
        // Debug builds also run the integration-test host (ASPNETCORE_ENVIRONMENT=Test),
        // which must behave like a clean, anonymous slate — otherwise every request made
        // through WebApplicationFactory<Program> would silently come in pre-authenticated
        // as admin, making it impossible to test real authentication/authorization behavior.
        if (environment.IsEnvironment("Test"))
        {
            await next(context);
            return;
        }

        if (context.Request.Path.Value?.Contains('.') == true)
        {
            await next(context);
            return;
        }

        // Always run as the hard-coded dev admin outside the Test host — even if a
        // stale real sign-in cookie (issued by an earlier manual login) is still
        // in the browser. Without this, that cookie authenticates first in
        // UseAuthentication() and this middleware would skip itself, leaving you
        // as whatever role that old cookie carried.
        var alreadyDevUser = context.User.Identity is { AuthenticationType: "DevAuth" };
        if (!alreadyDevUser)
        {
            var claims = new List<Claim>
            {
                // IMPORTANT: Guid as string
                new(ClaimTypes.NameIdentifier, FixedDevUserId.ToString()),
                new(ClaimTypes.Name, "dev-user"),
                new(ClaimTypes.Email, "dev@example.com"),
                new(ClaimTypes.Role, UserRole.Admin.ToString())
            };

            var identity = new ClaimsIdentity(claims, "DevAuth");
            context.User = new ClaimsPrincipal(identity);

            logger.LogInformation("Injected development user {UserId}", FixedDevUserId);
        }
#endif
        await next(context);
    }
}
