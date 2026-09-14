namespace Biltjuv.Web.Infrastructure.Persistence.Entities;

/// <summary>
/// A player's core stats — one row per <see cref="AppUserEntity"/>, created
/// lazily the first time they touch anything game-related. Shares its primary
/// key with the user it belongs to (a true 1:1), rather than carrying its own
/// surrogate id.
/// </summary>
public sealed class UserGameDataEntity
{
    public Guid UserId { get; set; }
    public AppUserEntity User { get; set; } = null!;

    public long Money { get; set; }
    public int Respect { get; set; }
    public int Health { get; set; }
    public int StolenCars { get; set; }

    /// <summary>
    /// The warehouse this player owns, referencing the Id of a
    /// <see cref="Biltjuv.Web.Infrastructure.Warehouses.WarehouseDefinition"/> in the JSON catalog.
    /// Null until they buy one; a warehouse is required to store stolen cars.
    /// </summary>
    public Guid? WarehouseId { get; set; }

    /// <summary>Earliest time the player may attempt another crime; null means "right now".</summary>
    public DateTime? NextStealUtc { get; set; }

    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
