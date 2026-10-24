using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure.Persistence;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Repositories;

[TestFixture]
public sealed class TablePlanRepositoryTest
{
  [Test]
  public async Task FindTablePlanAsync_AFestivalWithAreasTablesAndPlacedStations_AnswersEveryAreaInSortOrderWithItsContents()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var hallId = await AddAreaAsync(fixture.DbContext, seeded.FestivalId, "Halle", 2, true);
    var gardenId = await AddAreaAsync(fixture.DbContext, seeded.FestivalId, "Garten", 1, false);
    await AddTableAsync(fixture.DbContext, seeded.FestivalId, gardenId, "Tisch 2", false);
    await AddTableAsync(fixture.DbContext, seeded.FestivalId, gardenId, "Tisch 1", true);
    await AddTableAsync(fixture.DbContext, seeded.FestivalId, hallId, "Stammtisch", false);
    await PlaceAsync(fixture.DbContext, seeded.FestivalId, seeded.BarStationId, gardenId);
    await PlaceAsync(fixture.DbContext, seeded.FestivalId, seeded.KitchenStationId, gardenId);
    var otherFestivalId = await AddFestivalAsync(fixture.DbContext);
    await AddAreaAsync(fixture.DbContext, otherFestivalId, "Garten", 0, false);
    await using var readContext = fixture.CreateContext();

    IReadOnlyList<Area> tablePlan = await new TablePlanRepository(readContext).FindTablePlanAsync(seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(tablePlan.Select(area => area.Name),
                                  Is.EqualTo(new[]
                                             {
                                               "Garten",
                                               "Halle"
                                             }));
                      Assert.That(tablePlan[0].Tables.Select(table => table.Name),
                                  Is.EqualTo(new[]
                                             {
                                               "Tisch 1",
                                               "Tisch 2"
                                             }));
                      Assert.That(tablePlan[0].StationShapes.Select(shape => shape.FestivalStation.Station.Name),
                                  Is.EqualTo(new[]
                                             {
                                               "Kueche",
                                               "Theke"
                                             }));
                      Assert.That(tablePlan[1].IsHidden, Is.True);
                      Assert.That(tablePlan[1].Tables.Select(table => table.Name), Is.EqualTo(new[] { "Stammtisch" }));
                      Assert.That(tablePlan[1].StationShapes, Is.Empty);
                    });
  }

  [Test]
  public async Task IsAreaNameTakenAsync_TheNameInOtherCapitalsAtTheSameFestival_IsTaken()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    await AddAreaAsync(fixture.DbContext, seeded.FestivalId, "Garten", 1, true);

    var isTaken = await new TablePlanRepository(fixture.DbContext).IsAreaNameTakenAsync(seeded.FestivalId, "GARTEN", null, TestContext.CurrentContext.CancellationToken);

    Assert.That(isTaken, Is.True);
  }

  [Test]
  public async Task IsAreaNameTakenAsync_TheNameAtAnotherFestivalOnly_IsFree()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var otherFestivalId = await AddFestivalAsync(fixture.DbContext);
    await AddAreaAsync(fixture.DbContext, otherFestivalId, "Garten", 1, false);

    var isTaken = await new TablePlanRepository(fixture.DbContext).IsAreaNameTakenAsync(seeded.FestivalId, "Garten", null, TestContext.CurrentContext.CancellationToken);

    Assert.That(isTaken, Is.False);
  }

  [Test]
  public async Task IsAreaNameTakenAsync_OnlyTheAreaBeingSavedCarriesTheName_IsFree()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var gardenId = await AddAreaAsync(fixture.DbContext, seeded.FestivalId, "Garten", 1, false);

    var isTaken = await new TablePlanRepository(fixture.DbContext).IsAreaNameTakenAsync(seeded.FestivalId, "garten", gardenId, TestContext.CurrentContext.CancellationToken);

    Assert.That(isTaken, Is.False);
  }

  [Test]
  public async Task IsTableNameTakenAsync_AHiddenTableInAnotherAreaWithTheNameInOtherCapitals_IsTaken()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var hallId = await AddAreaAsync(fixture.DbContext, seeded.FestivalId, "Halle", 2, false);
    await AddTableAsync(fixture.DbContext, seeded.FestivalId, hallId, "Tisch 4", true);

    var isTaken = await new TablePlanRepository(fixture.DbContext).IsTableNameTakenAsync(seeded.FestivalId, "tisch 4", null, TestContext.CurrentContext.CancellationToken);

    Assert.That(isTaken, Is.True);
  }

  [Test]
  public async Task IsTableNameTakenAsync_OnlyTheTableBeingSavedCarriesTheName_IsFree()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var gardenId = await AddAreaAsync(fixture.DbContext, seeded.FestivalId, "Garten", 1, false);
    var tableId = await AddTableAsync(fixture.DbContext, seeded.FestivalId, gardenId, "Tisch 4", false);

    var isTaken = await new TablePlanRepository(fixture.DbContext).IsTableNameTakenAsync(seeded.FestivalId, "Tisch 4", tableId, TestContext.CurrentContext.CancellationToken);

    Assert.That(isTaken, Is.False);
  }

  [Test]
  public async Task FindAreaAsync_AnAreaOfAnotherFestival_ReturnsNothing()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var otherFestivalId = await AddFestivalAsync(fixture.DbContext);
    var strangerAreaId = await AddAreaAsync(fixture.DbContext, otherFestivalId, "Garten", 1, false);

    var area = await new TablePlanRepository(fixture.DbContext).FindAreaAsync(seeded.FestivalId, strangerAreaId, TestContext.CurrentContext.CancellationToken);

    Assert.That(area, Is.Null);
  }

  [Test]
  public async Task FindTableAsync_ATableOfAnotherFestival_ReturnsNothing()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var otherFestivalId = await AddFestivalAsync(fixture.DbContext);
    var strangerAreaId = await AddAreaAsync(fixture.DbContext, otherFestivalId, "Garten", 1, false);
    var strangerTableId = await AddTableAsync(fixture.DbContext, otherFestivalId, strangerAreaId, "Tisch 1", false);

    var table = await new TablePlanRepository(fixture.DbContext).FindTableAsync(seeded.FestivalId, strangerTableId, TestContext.CurrentContext.CancellationToken);

    Assert.That(table, Is.Null);
  }

  [Test]
  public async Task RemoveArea_WithTheTablesAndShapesItWasFoundWith_LeavesNothingOfTheAreaBehind()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var gardenId = await AddAreaAsync(fixture.DbContext, seeded.FestivalId, "Garten", 1, false);
    await AddTableAsync(fixture.DbContext, seeded.FestivalId, gardenId, "Tisch 1", false);
    await PlaceAsync(fixture.DbContext, seeded.FestivalId, seeded.KitchenStationId, gardenId);
    await using var writeContext = fixture.CreateContext();
    TablePlanRepository repository = new(writeContext);

    var garden = (await repository.FindAreaWithContentsAsync(seeded.FestivalId, gardenId, TestContext.CurrentContext.CancellationToken))!;
    repository.RemoveStationShapes(garden.StationShapes.ToList());
    repository.RemoveTables(garden.Tables.ToList());
    repository.RemoveArea(garden);
    await repository.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    await using var readContext = fixture.CreateContext();
    var areaCount = await readContext.Areas.CountAsync(TestContext.CurrentContext.CancellationToken);
    var tableCount = await readContext.Tables.CountAsync(TestContext.CurrentContext.CancellationToken);
    var shapeCount = await readContext.FestivalStationShapes.CountAsync(TestContext.CurrentContext.CancellationToken);
    var linkCount = await readContext.FestivalStations.CountAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(areaCount, Is.Zero);
                      Assert.That(tableCount, Is.Zero);
                      Assert.That(shapeCount, Is.Zero);
                      Assert.That(linkCount, Is.EqualTo(2));
                    });
  }

  [Test]
  public async Task AddStationShapeAsync_AStationAtTheFestival_IsFoundByItsLinkAfterTheChangesAreSaved()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var gardenId = await AddAreaAsync(fixture.DbContext, seeded.FestivalId, "Garten", 1, false);
    var link = await fixture.DbContext.FestivalStations.SingleAsync(candidate => candidate.FestivalId == seeded.FestivalId && candidate.StationId == seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);
    TablePlanRepository repository = new(fixture.DbContext);

    await repository.AddStationShapeAsync(new()
                                          {
                                            FestivalStationId = link.Id,
                                            FestivalId = seeded.FestivalId,
                                            AreaId = gardenId,
                                            X = 100,
                                            Y = 50,
                                            Width = 60,
                                            Height = 60,
                                            Rotation = -30.5
                                          },
                                          TestContext.CurrentContext.CancellationToken);
    await repository.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    await using var readContext = fixture.CreateContext();
    var shape = await new TablePlanRepository(readContext).FindStationShapeAsync(link.Id, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(shape?.AreaId, Is.EqualTo(gardenId));
                      Assert.That(shape?.Rotation, Is.EqualTo(-30.5));
                    });
  }

  [Test]
  public async Task FindStationShapeAsync_AStationNotOnThePlan_ReturnsNothing()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var link = await fixture.DbContext.FestivalStations.SingleAsync(candidate => candidate.FestivalId == seeded.FestivalId && candidate.StationId == seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    var shape = await new TablePlanRepository(fixture.DbContext).FindStationShapeAsync(link.Id, TestContext.CurrentContext.CancellationToken);

    Assert.That(shape, Is.Null);
  }

  [Test]
  public async Task RemoveStationShape_AStationOnThePlan_TakesItOffThePlanWhenTheChangesAreSaved()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var gardenId = await AddAreaAsync(fixture.DbContext, seeded.FestivalId, "Garten", 1, false);
    await PlaceAsync(fixture.DbContext, seeded.FestivalId, seeded.KitchenStationId, gardenId);
    var link = await fixture.DbContext.FestivalStations.SingleAsync(candidate => candidate.FestivalId == seeded.FestivalId && candidate.StationId == seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);
    TablePlanRepository repository = new(fixture.DbContext);

    repository.RemoveStationShape((await repository.FindStationShapeAsync(link.Id, TestContext.CurrentContext.CancellationToken))!);
    await repository.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    await using var readContext = fixture.CreateContext();
    var shapesLeft = await readContext.FestivalStationShapes.CountAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(shapesLeft, Is.Zero);
  }

  [Test]
  public void RemoveTables_NullTables_ThrowsArgumentNullException()
  {
    using SqliteInMemoryFixture fixture = new();

    Assert.That(() => new TablePlanRepository(fixture.DbContext).RemoveTables(null!), Throws.ArgumentNullException);
  }

  [Test]
  public void RemoveStationShapes_NullShapes_ThrowsArgumentNullException()
  {
    using SqliteInMemoryFixture fixture = new();

    Assert.That(() => new TablePlanRepository(fixture.DbContext).RemoveStationShapes(null!), Throws.ArgumentNullException);
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

  private async Task<Guid> AddAreaAsync(GastronomyAppDbContext dbContext, Guid festivalId, string name, int sortOrder, bool isHidden)
  {
    var areaId = Guid.NewGuid();

    dbContext.Areas.Add(new()
                        {
                          Id = areaId,
                          FestivalId = festivalId,
                          Name = name,
                          SortOrder = sortOrder,
                          IsHidden = isHidden
                        });
    await dbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return areaId;
  }

  private async Task<Guid> AddTableAsync(GastronomyAppDbContext dbContext, Guid festivalId, Guid areaId, string name, bool isHidden)
  {
    var tableId = Guid.NewGuid();

    dbContext.Tables.Add(new()
                         {
                           Id = tableId,
                           FestivalId = festivalId,
                           AreaId = areaId,
                           Name = name,
                           X = 10,
                           Y = 20,
                           Width = 80,
                           Height = 40,
                           Rotation = 0,
                           IsHidden = isHidden
                         });
    await dbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return tableId;
  }

  private async Task PlaceAsync(GastronomyAppDbContext dbContext, Guid festivalId, Guid stationId, Guid areaId)
  {
    var link = await dbContext.FestivalStations.SingleAsync(candidate => candidate.FestivalId == festivalId && candidate.StationId == stationId, TestContext.CurrentContext.CancellationToken);

    dbContext.FestivalStationShapes.Add(new()
                                        {
                                          FestivalStationId = link.Id,
                                          FestivalId = festivalId,
                                          AreaId = areaId,
                                          X = 100,
                                          Y = 50,
                                          Width = 60,
                                          Height = 60,
                                          Rotation = 0
                                        });
    await dbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }
}
