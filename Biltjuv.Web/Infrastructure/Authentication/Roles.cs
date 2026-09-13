using Biltjuv.Web.Infrastructure.Persistence.Entities;

namespace Biltjuv.Web.Infrastructure.Authentication;

/// <summary>
/// Role names as they appear in the auth cookie's role claim. Kept in sync with
/// <see cref="UserRole"/> (stored by name), so <c>[Authorize(Roles = ...)]</c>
/// and <c>User.IsInRole(...)</c> checks line up with the database value.
/// </summary>
public static class Roles
{
    public const string User = nameof(UserRole.User);
    public const string Admin = nameof(UserRole.Admin);
}
