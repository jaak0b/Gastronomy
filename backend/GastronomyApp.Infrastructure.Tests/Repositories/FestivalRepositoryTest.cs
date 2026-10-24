using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Persistence;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Repositories;

[TestFixture]
public sealed class FestivalRepositoryTest
{
  private readonly FestivalService _festivalService = new();

  private readonly DateTime _start = new(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);

  private async Task<Guid> AddFestivalAsync(GastronomyAppDbContext dbContext, string name, DateTime startsAtUtc, DateTime endsAtUtc, bool isHidden)
  {
    var festivalId = Guid.NewGuid();

    dbContext.Festivals.Add(new()
                            {
                              Id = festivalId,
                              Name = name,
                              StartsAtUtc = startsAtUtc,
                              EndsAtUtc = endsAtUtc,
                              NextOrderNumber = 1,
                              IsHidden = isHidden
                            });

    await dbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return festivalId;
  }

  [Test]
  public async Task FindRunningAsync_AMomentInsideOneOfSeveralFestivals_ReturnsThatFestival()
  {
    using SqliteInMemoryFixture fixture = new();
    await AddFestivalAsync(fixture.DbContext, "Fruehlingsfest", _start.AddMonths(-3), _start.AddMonths(-3).AddDays(2), false);
    var wantedId = await AddFestivalAsync(fixture.DbContext, "Sommerfest", _start, _start.AddDays(1), false);

    FestivalRepository repository = new(fixture.DbContext, new(), new());

    var found = await repository.FindRunningAsync(_start.AddHours(6), TestContext.CurrentContext.CancellationToken);

    Assert.That(found?.Id, Is.EqualTo(wantedId));
  }

  [Test]
  public async Task FindRunningAsync_TheOnlyFestivalCoveringTheMomentIsHidden_ReturnsNull()
  {
    using SqliteInMemoryFixture fixture = new();
    await AddFestivalAsync(fixture.DbContext, "Sommerfest", _start, _start.AddDays(1), true);

    FestivalRepository repository = new(fixture.DbContext, new(), new());

    var found = await repository.FindRunningAsync(_start.AddHours(6), TestContext.CurrentContext.CancellationToken);

    Assert.That(found, Is.Null);
  }

  [Test]
  public async Task FindRunningAsync_ReadsAFestivalBack_StampsBothMomentsAsUtc()
  {
    using SqliteInMemoryFixture fixture = new();
    await AddFestivalAsync(fixture.DbContext, "Sommerfest", _start, _start.AddDays(1), false);

    FestivalRepository repository = new(fixture.DbContext, new(), new());

    var found = await repository.FindRunningAsync(_start.AddHours(6), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(found!.StartsAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
                      Assert.That(found.EndsAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
                    });
  }

  [Test]
  public async Task ExistsAsync_AFestivalThatWasStored_AnswersTrue()
  {
    using SqliteInMemoryFixture fixture = new();
    var festivalId = await AddFestivalAsync(fixture.DbContext, "Sommerfest", _start, _start.AddDays(1), false);

    FestivalRepository repository = new(fixture.DbContext, new(), new());

    Assert.Multiple(async () =>
                    {
                      Assert.That(await repository.ExistsAsync(festivalId, TestContext.CurrentContext.CancellationToken), Is.True);
                      Assert.That(await repository.ExistsAsync(Guid.NewGuid(), TestContext.CurrentContext.CancellationToken), Is.False);
                    });
  }

  [Test]
  public async Task FindIdsNotEndedAsync_AFestivalThatIsOverAndAHiddenOne_ReturnsOnlyTheOneStillToCome()
  {
    using SqliteInMemoryFixture fixture = new();
    await AddFestivalAsync(fixture.DbContext, "Fruehlingsfest", _start.AddMonths(-3), _start.AddMonths(-3).AddDays(2), false);
    await AddFestivalAsync(fixture.DbContext, "Verstecktes Fest", _start, _start.AddDays(1), true);
    var wantedId = await AddFestivalAsync(fixture.DbContext, "Sommerfest", _start, _start.AddDays(1), false);

    FestivalRepository repository = new(fixture.DbContext, new(), new());

    IReadOnlyList<Guid> found = await repository.FindIdsNotEndedAsync(_start.AddHours(6), TestContext.CurrentContext.CancellationToken);

    Assert.That(found, Is.EqualTo(new[] { wantedId }));
  }

  [Test]
  public async Task FindByIdAsync_AFestivalThatIsNotThere_ReturnsNothing()
  {
    using SqliteInMemoryFixture fixture = new();
    await AddFestivalAsync(fixture.DbContext, "Sommerfest", _start, _start.AddDays(1), false);

    FestivalRepository repository = new(fixture.DbContext, new(), new());

    Assert.That(await repository.FindByIdAsync(Guid.NewGuid(), TestContext.CurrentContext.CancellationToken), Is.Null);
  }

  [Test]
  public async Task FindAllWithContentsAsync_AFestivalNothingHasBeenAddedTo_CountsZeroStationsAndItems()
  {
    using SqliteInMemoryFixture fixture = new();
    var festivalId = await AddFestivalAsync(fixture.DbContext, "Sommerfest", _start, _start.AddDays(1), false);

    FestivalRepository repository = new(fixture.DbContext, new(), new());

    IReadOnlyList<Festival> festivals = await repository.FindAllWithContentsAsync(TestContext.CurrentContext.CancellationToken);
    IReadOnlyDictionary<Guid, int> orderCounts = await repository.CountOrdersByFestivalAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(festivals.Select(festival => festival.Id), Is.EqualTo(new[] { festivalId }));
                      Assert.That(_festivalService.StationCountOf(festivals[0]), Is.EqualTo(0));
                      Assert.That(_festivalService.MenuItemCountOf(festivals[0]), Is.EqualTo(0));
                      Assert.That(orderCounts.GetValueOrDefault(festivalId), Is.EqualTo(0));
                    });
  }

  [Test]
  public async Task FindAllWithContentsAsync_AFestivalWithStationsAndAMenu_CountsWhatBelongsToIt()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    FestivalRepository repository = new(fixture.DbContext, new(), new());

    IReadOnlyList<Festival> festivals = await repository.FindAllWithContentsAsync(TestContext.CurrentContext.CancellationToken);
    IReadOnlyDictionary<Guid, int> orderCounts = await repository.CountOrdersByFestivalAsync(TestContext.CurrentContext.CancellationToken);

    var sommerfest = festivals.First(festival => festival.Id == seeded.FestivalId);

    Assert.Multiple(() =>
                    {
                      Assert.That(_festivalService.StationCountOf(sommerfest), Is.EqualTo(2));
                      Assert.That(_festivalService.MenuItemCountOf(sommerfest), Is.EqualTo(2));
                      Assert.That(orderCounts.GetValueOrDefault(seeded.FestivalId), Is.EqualTo(0));
                    });
  }

  [Test]
  public async Task CopyContentsAsync_AFestivalWithAMenu_CopiesItsStationsItemsAndAssignmentsAcross()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    FestivalRepository repository = new(fixture.DbContext, new(), new());
    var newFestivalId = Guid.NewGuid();

    await repository.AddAsync(new()
                              {
                                Id = newFestivalId,
                                Name = "Sommerfest 2027",
                                StartsAtUtc = _start.AddYears(1),
                                EndsAtUtc = _start.AddYears(1).AddDays(1),
                                NextOrderNumber = 1,
                                IsHidden = false
                              },
                              TestContext.CurrentContext.CancellationToken);
    await repository.CopyContentsAsync(seeded.FestivalId, newFestivalId, TestContext.CurrentContext.CancellationToken);
    await repository.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    IReadOnlyList<Festival> festivals = await repository.FindAllWithContentsAsync(TestContext.CurrentContext.CancellationToken);

    var copy = festivals.First(festival => festival.Id == newFestivalId);

    Assert.Multiple(() =>
                    {
                      Assert.That(_festivalService.StationCountOf(copy), Is.EqualTo(2));
                      Assert.That(_festivalService.MenuItemCountOf(copy), Is.EqualTo(2));
                    });
  }

  [Test]
  public async Task CopyContentsAsync_AFestivalWhoseItemsHaveSoldOut_PutsThemBackOnSaleAtTheCopy()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    foreach (var menuRow in fixture.DbContext.FestivalCatalogItems)
      menuRow.IsAvailable = false;

    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    FestivalRepository repository = new(fixture.DbContext, new(), new());
    var newFestivalId = await AddFestivalAsync(fixture.DbContext, "Sommerfest 2027", _start.AddYears(1), _start.AddYears(1).AddDays(1), false);

    await repository.CopyContentsAsync(seeded.FestivalId, newFestivalId, TestContext.CurrentContext.CancellationToken);
    await repository.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    await using var readContext = fixture.CreateContext();

    Assert.That(readContext.FestivalCatalogItems.Where(menuRow => menuRow.FestivalId == newFestivalId).Select(menuRow => menuRow.IsAvailable), Is.All.True);
  }

  [Test]
  public async Task CopyContentsAsync_AFestivalWithAPlan_CopiesItsShownAreasAndTablesAndTheStationsPlacedInThem()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var gardenId = await AddAreaAsync(fixture.DbContext, seeded.FestivalId, "Garten", 2, false);
    var hallId = await AddAreaAsync(fixture.DbContext, seeded.FestivalId, "Halle", 1, true);
    await AddTableAsync(fixture.DbContext, seeded.FestivalId, gardenId, "Tisch 1", false);
    await AddTableAsync(fixture.DbContext, seeded.FestivalId, gardenId, "Tisch 2", true);
    await AddTableAsync(fixture.DbContext, seeded.FestivalId, hallId, "Stammtisch", false);
    await PlaceAsync(fixture.DbContext, seeded.FestivalId, seeded.KitchenStationId, gardenId);
    await PlaceAsync(fixture.DbContext, seeded.FestivalId, seeded.BarStationId, hallId);

    FestivalRepository repository = new(fixture.DbContext, new(), new());
    var newFestivalId = await AddFestivalAsync(fixture.DbContext, "Sommerfest 2027", _start.AddYears(1), _start.AddYears(1).AddDays(1), false);

    await repository.CopyContentsAsync(seeded.FestivalId, newFestivalId, TestContext.CurrentContext.CancellationToken);
    await repository.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    await using var readContext = fixture.CreateContext();
    var copiedArea = await readContext.Areas.Include(area => area.Tables).Include(area => area.StationShapes).ThenInclude(shape => shape.FestivalStation).SingleAsync(area => area.FestivalId == newFestivalId, TestContext.CurrentContext.CancellationToken);
    var copiedTable = copiedArea.Tables.Single();
    var copiedShape = copiedArea.StationShapes.Single();

    Assert.Multiple(() =>
                    {
                      Assert.That(copiedArea.Id, Is.Not.EqualTo(gardenId));
                      Assert.That(copiedArea.Name, Is.EqualTo("Garten"));
                      Assert.That(copiedArea.SortOrder, Is.EqualTo(2));
                      Assert.That(copiedArea.IsHidden, Is.False);
                      Assert.That(copiedTable.Name, Is.EqualTo("Tisch 1"));
                      Assert.That(copiedTable.FestivalId, Is.EqualTo(newFestivalId));
                      Assert.That(copiedTable.X, Is.EqualTo(10.5));
                      Assert.That(copiedTable.Y, Is.EqualTo(-20.25));
                      Assert.That(copiedTable.Width, Is.EqualTo(80));
                      Assert.That(copiedTable.Height, Is.EqualTo(40.75));
                      Assert.That(copiedTable.Rotation, Is.EqualTo(-12.5));
                      Assert.That(copiedShape.FestivalStation.FestivalId, Is.EqualTo(newFestivalId));
                      Assert.That(copiedShape.FestivalStation.StationId, Is.EqualTo(seeded.KitchenStationId));
                      Assert.That(copiedShape.X, Is.EqualTo(100.5));
                      Assert.That(copiedShape.Y, Is.EqualTo(50.25));
                      Assert.That(copiedShape.Width, Is.EqualTo(60));
                      Assert.That(copiedShape.Height, Is.EqualTo(30));
                      Assert.That(copiedShape.Rotation, Is.EqualTo(-90));
                    });
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

  private async Task AddTableAsync(GastronomyAppDbContext dbContext, Guid festivalId, Guid areaId, string name, bool isHidden)
  {
    dbContext.Tables.Add(new()
                         {
                           Id = Guid.NewGuid(),
                           FestivalId = festivalId,
                           AreaId = areaId,
                           Name = name,
                           X = 10.5,
                           Y = -20.25,
                           Width = 80,
                           Height = 40.75,
                           Rotation = -12.5,
                           IsHidden = isHidden
                         });
    await dbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }

  private async Task PlaceAsync(GastronomyAppDbContext dbContext, Guid festivalId, Guid stationId, Guid areaId)
  {
    var link = await dbContext.FestivalStations.SingleAsync(candidate => candidate.FestivalId == festivalId && candidate.StationId == stationId, TestContext.CurrentContext.CancellationToken);

    dbContext.FestivalStationShapes.Add(new()
                                        {
                                          FestivalStationId = link.Id,
                                          FestivalId = festivalId,
                                          AreaId = areaId,
                                          X = 100.5,
                                          Y = 50.25,
                                          Width = 60,
                                          Height = 30,
                                          Rotation = -90
                                        });
    await dbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }
}
