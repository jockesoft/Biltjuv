namespace Biltjuv.Web.Infrastructure.Persistence.Entities;

/// <summary>
/// Access level for an <see cref="AppUserEntity"/>. Stored as its name in the
/// <c>role</c> column; new passwordless accounts are provisioned as
/// <see cref="User"/> and promoted to <see cref="Admin"/> by editing the row
/// directly in the database.
/// </summary>
public enum UserRole
{
    /// <summary>Signed in, but no admin access. The default for every new account.</summary>
    User = 0,

    /// <summary>Full access, including the admin dashboard.</summary>
    Admin = 1
}
