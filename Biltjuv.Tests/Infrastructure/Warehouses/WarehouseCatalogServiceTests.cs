using AwesomeAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Biltjuv.Web.Infrastructure.Caching;
using Biltjuv.Web.Infrastructure.Warehouses;

namespace Biltjuv.Tests.Infrastructure.Warehouses;

[TestFixture]
public sealed class WarehouseCatalogServiceTests
{
    private Mock<IDistributedCacheJson> _cache = null!;
    private Mock<IHostEnvironment> _environment = null!;
    private WarehouseOptions _options = null!;
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _cache = new Mock<IDistributedCacheJson>();

        _tempDir = Path.Combine(Path.GetTempPath(), "biltjuv-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _environment = new Mock<IHostEnvironment>();
        _environment.SetupGet(x => x.ContentRootPath).Returns(_tempDir);

        _options = new WarehouseOptions { FilePath = "warehouses.json", CacheTtlHours = 24 };
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private WarehouseCatalogService CreateSut() => new(
        _cache.Object,
        Options.Create(_options),
        _environment.Object,
        NullLogger<WarehouseCatalogService>.Instance);

    private void WriteCatalogFile(string json) =>
        File.WriteAllText(Path.Combine(_tempDir, _options.FilePath), json);

    private static WarehouseDefinition MakeWarehouse(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Name = "Koja",
        Space = 10,
        Price = 25000,
        MinLevel = 1,
        MaxSteal = 3,
        CreatedUtc = new DateTime(2021, 2, 27, 11, 24, 8, DateTimeKind.Utc)
    };

    // ---- GetAllAsync ------------------------------------------------------

    [Test]
    public async Task GetAllAsync_Should_ReturnCachedCatalog_WithoutTouchingTheFile()
    {
        var cached = new List<WarehouseDefinition> { MakeWarehouse() };
        _cache.Setup(x => x.GetAsync<List<WarehouseDefinition>>("warehouses:catalog", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cached);
        // Deliberately no file written to _tempDir — a fall-through to disk would throw/return empty.

        var result = await CreateSut().GetAllAsync();

        result.Should().BeEquivalentTo(cached);
        _cache.Verify(x => x.SetAsync(
            It.IsAny<string>(), It.IsAny<List<WarehouseDefinition>>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task GetAllAsync_Should_LoadFromFile_AndPopulateCache_OnCacheMiss()
    {
        _cache.Setup(x => x.GetAsync<List<WarehouseDefinition>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<WarehouseDefinition>?)null);
        WriteCatalogFile("""
            [
              {
                "Id": "b5af8537-190f-40be-bfd5-1460da13bbc1",
                "Name": "Koja",
                "Space": 10,
                "Price": 25000,
                "MinLevel": 1,
                "MaxSteal": 3,
                "CreatedUtc": "2021-02-27T11:24:08Z"
              }
            ]
            """);

        var result = await CreateSut().GetAllAsync();

        result.Should().ContainSingle();
        result[0].Id.Should().Be(Guid.Parse("b5af8537-190f-40be-bfd5-1460da13bbc1"));
        result[0].Name.Should().Be("Koja");
        result[0].Space.Should().Be(10);
        result[0].Price.Should().Be(25000);
        result[0].MinLevel.Should().Be(1);
        result[0].MaxSteal.Should().Be(3);

        _cache.Verify(x => x.SetAsync(
            "warehouses:catalog",
            It.Is<List<WarehouseDefinition>>(l => l.Count == 1),
            TimeSpan.FromHours(24),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task GetAllAsync_Should_ReturnEmptyList_WhenCatalogFileIsMissing()
    {
        _cache.Setup(x => x.GetAsync<List<WarehouseDefinition>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<WarehouseDefinition>?)null);
        // No file written — path does not exist.

        var result = await CreateSut().GetAllAsync();

        result.Should().BeEmpty();
    }

    [Test]
    public async Task GetAllAsync_Should_UseConfiguredTtl_WhenPopulatingCache()
    {
        _options.CacheTtlHours = 6;
        _cache.Setup(x => x.GetAsync<List<WarehouseDefinition>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<WarehouseDefinition>?)null);
        WriteCatalogFile("[]");

        await CreateSut().GetAllAsync();

        _cache.Verify(x => x.SetAsync(
            It.IsAny<string>(), It.IsAny<List<WarehouseDefinition>>(), TimeSpan.FromHours(6), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ---- GetByIdAsync -------------------------------------------------------

    [Test]
    public async Task GetByIdAsync_Should_ReturnMatchingWarehouse()
    {
        var targetId = Guid.NewGuid();
        var cached = new List<WarehouseDefinition> { MakeWarehouse(targetId), MakeWarehouse() };
        _cache.Setup(x => x.GetAsync<List<WarehouseDefinition>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cached);

        var result = await CreateSut().GetByIdAsync(targetId);

        result.Should().NotBeNull();
        result!.Id.Should().Be(targetId);
    }

    [Test]
    public async Task GetByIdAsync_Should_ReturnNull_WhenNoWarehouseMatches()
    {
        var cached = new List<WarehouseDefinition> { MakeWarehouse() };
        _cache.Setup(x => x.GetAsync<List<WarehouseDefinition>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cached);

        var result = await CreateSut().GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }
}
