namespace Biltjuv.Web.Infrastructure.Warehouses;

/// <summary>Bound from the <c>Warehouses</c> configuration section — tunes the warehouse catalog cache.</summary>
public sealed class WarehouseOptions
{
    public const string SectionName = "Warehouses";

    /// <summary>Path to the warehouse catalog JSON file, relative to the app's content root.</summary>
    public string FilePath { get; set; } = "Config/warehouses.json";

    /// <summary>How long the catalog stays cached in Redis before a lazy reload from disk.</summary>
    public int CacheTtlHours { get; set; } = 24;
}
