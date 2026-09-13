namespace Biltjuv.Web.Services;

/// <summary>Reads the signed-in user's identity from the current request's auth cookie.</summary>
public interface ICurrentUserService
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    string? Email { get; }
    string? DisplayName { get; }
    string? Role { get; }
    bool IsAdmin { get; }
    bool IsInRole(string role);
}
