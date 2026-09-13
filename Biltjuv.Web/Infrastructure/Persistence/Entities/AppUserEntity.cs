namespace Biltjuv.Web.Infrastructure.Persistence.Entities;

public sealed class AppUserEntity
{
    public Guid Id { get; set; }

    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>Access level. Defaults to <see cref="UserRole.User"/> on creation.</summary>
    public UserRole Role { get; set; } = UserRole.User;

    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    public ICollection<LoginTokenEntity> LoginTokens { get; set; } = new List<LoginTokenEntity>();
    public UserGameDataEntity? GameData { get; set; }
}
