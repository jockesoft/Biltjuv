namespace Biltjuv.Web.Infrastructure.Warehouses;

/// <summary>
/// One purchasable warehouse tier, as defined in the warehouses JSON catalog.
/// Not stored in Postgres — a player's <c>user_game_data.warehouse_id</c> simply
/// references <see cref="Id"/> from this catalog.
/// </summary>
public sealed class WarehouseDefinition
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>How many stolen cars can be stored before the warehouse is full.</summary>
    public int Space { get; set; }

    /// <summary>Purchase price.</summary>
    public long Price { get; set; }

    /// <summary>Player level required to purchase this warehouse.</summary>
    public int MinLevel { get; set; }

    /// <summary>Max cars that can be stolen in a single attempt when this warehouse is owned.</summary>
    public int MaxSteal { get; set; }

    public DateTime CreatedUtc { get; set; }
}
