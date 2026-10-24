using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure.Configurations;
using GastronomyApp.Infrastructure.Persistence;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Configurations;

[TestFixture]
public sealed class TableConfigurationTest
{
  [Test]
  public void Configure_NullBuilder_ThrowsArgumentNullException()
  {
    TableConfiguration configuration = new();

    Assert.That(() => configuration.Configure(null!), Throws.ArgumentNullException);
  }

  [Test]
  public async Task Save_ANameAHiddenTableInAnotherAreaOfTheFestivalCarriesInOtherCapitals_IsRefusedByTheIndex()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var gardenId = await AddAreaAsync(fixture.DbContext, seeded.FestivalId, "Garten");
    var hallId = await AddAreaAsync(fixture.DbContext, seeded.FestivalId, "Halle");
    fixture.DbContext.Tables.Add(BuildTable(seeded.FestivalId, gardenId, "Tisch 4", true));
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    await using var second = fixture.CreateContext();
    second.Tables.Add(BuildTable(seeded.FestivalId, hallId, "TISCH 4", false));

    Assert.ThrowsAsync<DbUpdateException>(async () => await second.SaveChangesAsync(TestContext.CurrentContext.CancellationToken));
  }

  [Test]
  public async Task Save_ATableWhoseFestivalDiffersFromItsAreas_IsRefusedByTheDatabase()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var gardenId = await AddAreaAsync(fixture.DbContext, seeded.FestivalId, "Garten");
    var otherFestivalId = await AddFestivalAsync(fixture.DbContext);

    await using var second = fixture.CreateContext();
    second.Tables.Add(BuildTable(otherFestivalId, gardenId, "Tisch 1", false));

    var refused = Assert.ThrowsAsync<DbUpdateException>(async () => await second.SaveChangesAsync(TestContext.CurrentContext.CancellationToken));

    Assert.That(refused!.InnerException!.Message, Does.Contain("FOREIGN KEY"));
  }

  [Test]
  public async Task Save_PositionSizeAndRotationWithManyDecimalsAndANegativeAngle_ReadsBackUnchanged()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var gardenId = await AddAreaAsync(fixture.DbContext, seeded.FestivalId, "Garten");
    var table = BuildTable(seeded.FestivalId, gardenId, "Tisch 1", false);
    table.X = -0.1234567890123;
    table.Y = 98765.4321;
    table.Width = 1e-9;
    table.Height = 123456789.987654;
    table.Rotation = -179.999999;
    fixture.DbContext.Tables.Add(table);
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    await using var readContext = fixture.CreateContext();
    var stored = await readContext.Tables.SingleAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(stored.X, Is.EqualTo(-0.1234567890123));
                      Assert.That(stored.Y, Is.EqualTo(98765.4321));
                      Assert.That(stored.Width, Is.EqualTo(1e-9));
                      Assert.That(stored.Height, Is.EqualTo(123456789.987654));
                      Assert.That(stored.Rotation, Is.EqualTo(-179.999999));
                    });
  }

  private async Task<Guid> AddFestivalAsync(GastronomyAppDbContext dbContext)
  {
    var festivalId = Guid.NewGuid();

    dbContext.Festivals.Add(new()
                            {
                              Id = festivalId,
                              Name = "Herbstfest",
                              StartsAtUtc = new(2030, 10, 1, 12, 0, 0, DateTimeKind.Utc),
                              EndsAtUtc = new(2030, 10, 2, 12, 0, 0, DateTimeKind.Utc),
                              NextOrderNumber = 1,
                              IsHidden = false
                            });
    await dbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return festivalId;
  }

  private async Task<Guid> AddAreaAsync(GastronomyAppDbContext dbContext, Guid festivalId, string name)
  {
    var areaId = Guid.NewGuid();

    dbContext.Areas.Add(new()
                        {
                          Id = areaId,
                          FestivalId = festivalId,
                          Name = name,
                          SortOrder = 1,
                          IsHidden = false
                        });
    await dbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return areaId;
  }

  private Table BuildTable(Guid festivalId, Guid areaId, string name, bool isHidden)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             FestivalId = festivalId,
             AreaId = areaId,
             Name = name,
             X = 10,
             Y = 20,
             Width = 80,
             Height = 40,
             Rotation = 0,
             IsHidden = isHidden
           };
  }
}
