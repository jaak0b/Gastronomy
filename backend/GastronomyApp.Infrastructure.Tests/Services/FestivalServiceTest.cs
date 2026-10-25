using GastronomyApp.Contracts.Admin.Festivals;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Persistence;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Services;

[TestFixture]
public sealed class FestivalServiceTest
{
  [SetUp]
  public async Task SetUp()
  {
    _fixture = new();
    _seeded = await new DomainSeeder().SeedAsync(_fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    _service = CreateService(_fixture.DbContext);
  }

  [TearDown]
  public void TearDown()
  {
    _fixture.Dispose();
  }

  private readonly DateTime _start = new(2030, 6, 1, 14, 0, 0, DateTimeKind.Utc);

  private SqliteInMemoryFixture _fixture = null!;
  private SeededDomain _seeded = null!;
  private FestivalService _service = null!;

  [Test]
  public async Task ListAsync_AFestivalWithStationsAndAMenu_CountsWhatBelongsToItAndSaysItRuns()
  {
    var listed = await _service.ListAsync(TestContext.CurrentContext.CancellationToken);

    var seededRow = listed.Festivals.Single();

    Assert.Multiple(() =>
                    {
                      Assert.That(seededRow.FestivalId, Is.EqualTo(_seeded.FestivalId));
                      Assert.That(seededRow.Name, Is.EqualTo("Sommerfest"));
                      Assert.That(seededRow.StartsAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
                      Assert.That(seededRow.EndsAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
                      Assert.That(seededRow.StationCount, Is.EqualTo(2));
                      Assert.That(seededRow.MenuItemCount, Is.EqualTo(2));
                      Assert.That(seededRow.OrderCount, Is.Zero);
                      Assert.That(seededRow.IsHidden, Is.False);
                      Assert.That(seededRow.IsRunning, Is.True);
                    });
  }

  [Test]
  public async Task ListAsync_AFestivalNothingHasBeenAddedTo_CountsNothingAndListsTheLatestStartFirst()
  {
    var created = await _service.CreateAsync(Request("Herbstfest", _start, _start.AddHours(6)), TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var listed = await _service.ListAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(listed.Festivals.Select(festival => festival.FestivalId),
                                  Is.EqualTo(new[]
                                             {
                                               created.Value.FestivalId,
                                               _seeded.FestivalId
                                             }));
                      Assert.That(listed.Festivals[0].StationCount, Is.Zero);
                      Assert.That(listed.Festivals[0].MenuItemCount, Is.Zero);
                      Assert.That(listed.Festivals[0].IsRunning, Is.False);
                    });
  }

  [Test]
  public async Task ListAsync_AHiddenFestivalInsideItsPeriod_IsListedButNotRunning()
  {
    var festival = await _fixture.DbContext.Festivals.SingleAsync(TestContext.CurrentContext.CancellationToken);
    festival.IsHidden = true;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var listed = await _service.ListAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(listed.Festivals.Single().IsHidden, Is.True);
                      Assert.That(listed.Festivals.Single().IsRunning, Is.False);
                    });
  }

  [Test]
  public async Task ListAsync_AFestivalWithOrders_CountsThem()
  {
    var tableId = await new DomainSeeder().SeedTableAsync(_fixture.DbContext, _seeded.FestivalId, "Tisch 1", TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.Orders.AddRange(BuildOrder(1, tableId), BuildOrder(2, tableId));
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var listed = await _service.ListAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(listed.Festivals.Single().OrderCount, Is.EqualTo(2));
  }

  [Test]
  public async Task CreateAsync_AnEndThatIsNotAfterTheStart_IsRefusedBecauseThePeriodIsInvalid()
  {
    var created = await _service.CreateAsync(Request("Sommerfest", _start, _start), TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    Assert.Multiple(() =>
                    {
                      Assert.That(created.IsError, Is.True);
                      Assert.That(created.RefusalMessageKey(), Is.EqualTo("errors.admin.festivals.periodInvalid"));
                      Assert.That(_fixture.DbContext.Festivals.Count(), Is.EqualTo(1));
                    });
  }

  [Test]
  public async Task CreateAsync_APeriodAnotherFestivalCovers_NamesTheFestivalInTheWayAndStoresNothing()
  {
    var created = await _service.CreateAsync(Request("Herbstfest", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2)), TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    Assert.Multiple(() =>
                    {
                      Assert.That(created.IsError, Is.True);
                      Assert.That(created.FirstError.NumericType, Is.EqualTo(RefusalType.Conflict));
                      Assert.That(created.RefusalMessageKey(), Is.EqualTo("errors.admin.festivals.overlaps"));
                      Assert.That(created.RefusalMetadata("name"), Is.EqualTo("Sommerfest"));
                      Assert.That(_fixture.DbContext.Festivals.Count(), Is.EqualTo(1));
                    });
  }

  [Test]
  public async Task CreateAsync_APeriodThatOnlyTouchesTheEndOfAnotherFestival_IsAllowed()
  {
    var seededEnd = (await _fixture.DbContext.Festivals.AsNoTracking().SingleAsync(TestContext.CurrentContext.CancellationToken)).EndsAtUtc;

    var created = await _service.CreateAsync(Request("Herbstfest", seededEnd, seededEnd.AddHours(6)), TestContext.CurrentContext.CancellationToken);

    Assert.That(created.IsError, Is.False);
  }

  [Test]
  public async Task CreateAsync_APeriodNoOtherFestivalCovers_StoresTheFestivalAsShownWithOrderNumbersAtOne()
  {
    var created = await _service.CreateAsync(Request("Herbstfest", _start, _start.AddHours(6)), TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var stored = await _fixture.DbContext.Festivals.SingleAsync(festival => festival.Id == created.Value.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(stored.Name, Is.EqualTo("Herbstfest"));
                      Assert.That(stored.StartsAtUtc, Is.EqualTo(_start));
                      Assert.That(stored.EndsAtUtc, Is.EqualTo(_start.AddHours(6)));
                      Assert.That(stored.IsHidden, Is.False);
                      Assert.That(stored.NextOrderNumber, Is.EqualTo(1));
                    });
  }

  [Test]
  public async Task CreateAsync_TwoStoredIngredients_StoresAnUnlimitedStockRowForEach()
  {
    var ingredientIds = await AddIngredientsAsync();

    var created = await _service.CreateAsync(Request("Herbstfest", _start, _start.AddHours(10)), TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var stored = await _fixture.DbContext.FestivalIngredients.Where(stock => stock.FestivalId == created.Value.FestivalId).ToListAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(stored.Select(stock => stock.IngredientId), Is.EquivalentTo(ingredientIds));
                      Assert.That(stored.All(stock => stock.AvailableAmount == null), Is.True);
                    });
  }

  [Test]
  public async Task UpdateAsync_AFestivalThatIsNotThere_IsRefusedBecauseTheFestivalIsNotFound()
  {
    var updated = await _service.UpdateAsync(Guid.NewGuid(), Request("Sommerfest", _start, _start.AddHours(6)), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(updated.IsError, Is.True);
                      Assert.That(updated.RefusalMessageKey(), Is.EqualTo("FestivalNotFound"));
                    });
  }

  [Test]
  public async Task UpdateAsync_ANewNameAndAPeriodOverlappingItsOwn_StoresBoth()
  {
    var newEnd = DateTime.UtcNow.AddYears(2);

    var updated = await _service.UpdateAsync(_seeded.FestivalId, Request("Sommerfest 2", DateTime.UtcNow.AddDays(-2), newEnd), TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var stored = await _fixture.DbContext.Festivals.SingleAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(updated.Value, Is.EqualTo(new SavedFestivalView(_seeded.FestivalId)));
                      Assert.That(stored.Name, Is.EqualTo("Sommerfest 2"));
                      Assert.That(stored.EndsAtUtc, Is.EqualTo(newEnd));
                    });
  }

  [Test]
  public async Task UpdateAsync_APeriodAnotherFestivalCovers_IsRefusedAndKeepsTheOldPeriod()
  {
    var other = await _service.CreateAsync(Request("Herbstfest", _start, _start.AddHours(6)), TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var updated = await _service.UpdateAsync(_seeded.FestivalId, Request("Sommerfest", _start.AddHours(1), _start.AddHours(2)), TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    Assert.Multiple(() =>
                    {
                      Assert.That(updated.RefusalMessageKey(), Is.EqualTo("errors.admin.festivals.overlaps"));
                      Assert.That(updated.RefusalMetadata("name"), Is.EqualTo("Herbstfest"));
                      Assert.That(_fixture.DbContext.Festivals.Single(festival => festival.Id == _seeded.FestivalId).StartsAtUtc, Is.Not.EqualTo(_start.AddHours(1)));
                      Assert.That(other.IsError, Is.False);
                    });
  }

  [Test]
  public async Task CopyAsync_AFestivalThatIsNotThere_IsRefusedBecauseTheFestivalIsNotFound()
  {
    var copied = await _service.CopyAsync(Guid.NewGuid(), Request("Sommerfest 2027", _start, _start.AddHours(6)), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(copied.IsError, Is.True);
                      Assert.That(copied.RefusalMessageKey(), Is.EqualTo("FestivalNotFound"));
                    });
  }

  [Test]
  public async Task CopyAsync_APeriodAnotherFestivalCovers_IsRefusedAndStoresNothing()
  {
    var copied = await _service.CopyAsync(_seeded.FestivalId, Request("Sommerfest 2027", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2)), TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    Assert.Multiple(() =>
                    {
                      Assert.That(copied.RefusalMessageKey(), Is.EqualTo("errors.admin.festivals.overlaps"));
                      Assert.That(_fixture.DbContext.Festivals.Count(), Is.EqualTo(1));
                      Assert.That(_fixture.DbContext.FestivalStations.Count(), Is.EqualTo(2));
                    });
  }

  [Test]
  public async Task CopyAsync_AFestivalWithAMenu_CopiesItsStationsMenuAndAssignmentsAcrossAndStartsTheCountersAtOne()
  {
    var link = await _fixture.DbContext.FestivalStations.FirstAsync(TestContext.CurrentContext.CancellationToken);
    link.NextStationOrderNumber = 9;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    var copied = await _service.CopyAsync(_seeded.FestivalId, Request("Sommerfest 2027", _start, _start.AddHours(6)), TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var copyId = copied.Value.FestivalId;
    var copiedFestival = await _fixture.DbContext.Festivals.SingleAsync(festival => festival.Id == copyId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(copyId, Is.Not.EqualTo(_seeded.FestivalId));
                      Assert.That(copiedFestival.Name, Is.EqualTo("Sommerfest 2027"));
                      Assert.That(copiedFestival.NextOrderNumber, Is.EqualTo(1));
                      Assert.That(_fixture.DbContext.FestivalStations.Where(station => station.FestivalId == copyId).Select(station => station.StationId),
                                  Is.EquivalentTo(new[]
                                                  {
                                                    _seeded.KitchenStationId,
                                                    _seeded.BarStationId
                                                  }));
                      Assert.That(_fixture.DbContext.FestivalStations.Where(station => station.FestivalId == copyId).Select(station => station.NextStationOrderNumber), Is.All.EqualTo(1));
                      Assert.That(_fixture.DbContext.FestivalCatalogItems.Where(menuRow => menuRow.FestivalId == copyId).Select(menuRow => menuRow.PriceCents),
                                  Is.EquivalentTo(new[]
                                                  {
                                                    350,
                                                    250
                                                  }));
                      Assert.That(_fixture.DbContext.ItemStationAssignments.Where(assignment => assignment.FestivalId == copyId).Select(assignment => assignment.StationId),
                                  Is.EquivalentTo(new[]
                                                  {
                                                    _seeded.KitchenStationId,
                                                    _seeded.BarStationId
                                                  }));
                      Assert.That(_fixture.DbContext.FestivalStations.Count(station => station.FestivalId == _seeded.FestivalId), Is.EqualTo(2));
                    });
  }

  [Test]
  public async Task CopyAsync_AFestivalWhoseItemsHaveSoldOut_PutsThemBackOnSaleAtTheCopy()
  {
    foreach (var menuRow in _fixture.DbContext.FestivalCatalogItems)
      menuRow.IsAvailable = false;

    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    var copied = await _service.CopyAsync(_seeded.FestivalId, Request("Sommerfest 2027", _start, _start.AddHours(6)), TestContext.CurrentContext.CancellationToken);

    await using var readContext = _fixture.CreateContext();

    Assert.That(readContext.FestivalCatalogItems.Where(menuRow => menuRow.FestivalId == copied.Value.FestivalId).Select(menuRow => menuRow.IsAvailable), Is.All.True);
  }

  [Test]
  public async Task CopyAsync_AFestivalWithAPlan_CopiesItsShownAreasAndTablesAndTheStationsPlacedInThem()
  {
    var gardenId = await AddAreaAsync("Garten", 2, false);
    var hallId = await AddAreaAsync("Halle", 1, true);
    await AddTableAsync(gardenId, "Tisch 1", false);
    await AddTableAsync(gardenId, "Tisch 2", true);
    await AddTableAsync(hallId, "Stammtisch", false);
    await PlaceAsync(_seeded.KitchenStationId, gardenId);
    await PlaceAsync(_seeded.BarStationId, hallId);

    var copied = await _service.CopyAsync(_seeded.FestivalId, Request("Sommerfest 2027", _start, _start.AddHours(6)), TestContext.CurrentContext.CancellationToken);

    await using var readContext = _fixture.CreateContext();
    var copyId = copied.Value.FestivalId;
    var copiedArea = await readContext.Areas.Include(area => area.Tables).Include(area => area.StationShapes).ThenInclude(shape => shape.FestivalStation).SingleAsync(area => area.FestivalId == copyId, TestContext.CurrentContext.CancellationToken);
    var copiedTable = copiedArea.Tables.Single();
    var copiedShape = copiedArea.StationShapes.Single();

    Assert.Multiple(() =>
                    {
                      Assert.That(copiedArea.Id, Is.Not.EqualTo(gardenId));
                      Assert.That(copiedArea.Name, Is.EqualTo("Garten"));
                      Assert.That(copiedArea.SortOrder, Is.EqualTo(2));
                      Assert.That(copiedArea.IsHidden, Is.False);
                      Assert.That(copiedTable.Name, Is.EqualTo("Tisch 1"));
                      Assert.That(copiedTable.FestivalId, Is.EqualTo(copyId));
                      Assert.That(copiedTable.X, Is.EqualTo(10.5));
                      Assert.That(copiedTable.Y, Is.EqualTo(-20.25));
                      Assert.That(copiedTable.Width, Is.EqualTo(80));
                      Assert.That(copiedTable.Height, Is.EqualTo(40.75));
                      Assert.That(copiedTable.Rotation, Is.EqualTo(-12.5));
                      Assert.That(copiedShape.FestivalStation.FestivalId, Is.EqualTo(copyId));
                      Assert.That(copiedShape.FestivalStation.StationId, Is.EqualTo(_seeded.KitchenStationId));
                      Assert.That(copiedShape.X, Is.EqualTo(100.5));
                      Assert.That(copiedShape.Y, Is.EqualTo(50.25));
                      Assert.That(copiedShape.Width, Is.EqualTo(60));
                      Assert.That(copiedShape.Height, Is.EqualTo(30));
                      Assert.That(copiedShape.Rotation, Is.EqualTo(-90));
                    });
  }

  [Test]
  public async Task CopyAsync_TheSourceHasAnAvailableAmount_StoresUnlimitedRowsForTheCopy()
  {
    var ingredientIds = await AddIngredientsAsync();
    var source = await _service.CreateAsync(Request("Herbstfest", _start, _start.AddHours(10)), TestContext.CurrentContext.CancellationToken);
    var sourceRow = await _fixture.DbContext.FestivalIngredients.FirstAsync(stock => stock.FestivalId == source.Value.FestivalId, TestContext.CurrentContext.CancellationToken);
    sourceRow.AvailableAmount = 250;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    var copy = await _service.CopyAsync(source.Value.FestivalId, Request("Frühlingsfest", _start.AddDays(90), _start.AddDays(90).AddHours(10)), TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var stored = await _fixture.DbContext.FestivalIngredients.Where(stock => stock.FestivalId == copy.Value.FestivalId).ToListAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(stored.Select(stock => stock.IngredientId), Is.EquivalentTo(ingredientIds));
                      Assert.That(stored.All(stock => stock.AvailableAmount == null), Is.True);
                    });
  }

  [Test]
  public async Task HideAsync_AFestivalThatIsNotThere_IsRefusedBecauseTheFestivalIsNotFound()
  {
    var hidden = await _service.HideAsync(Guid.NewGuid(), TestContext.CurrentContext.CancellationToken);

    Assert.That(hidden.RefusalMessageKey(), Is.EqualTo("FestivalNotFound"));
  }

  [Test]
  public async Task HideAsync_AFestivalRunningRightNow_IsRefusedAndNamesTheFestival()
  {
    var hidden = await _service.HideAsync(_seeded.FestivalId, TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    Assert.Multiple(() =>
                    {
                      Assert.That(hidden.IsError, Is.True);
                      Assert.That(hidden.FirstError.NumericType, Is.EqualTo(RefusalType.Conflict));
                      Assert.That(hidden.RefusalMessageKey(), Is.EqualTo("errors.admin.festivals.runningFestivalStaysShown"));
                      Assert.That(hidden.RefusalDescription(), Does.Contain(_seeded.FestivalId.ToString()));
                      Assert.That(_fixture.DbContext.Festivals.Single().IsHidden, Is.False);
                    });
  }

  [Test]
  public async Task HideAsync_AFestivalThatIsNotRunning_HidesItAndStoresIt()
  {
    var created = await _service.CreateAsync(Request("Herbstfest", _start, _start.AddHours(6)), TestContext.CurrentContext.CancellationToken);

    var hidden = await _service.HideAsync(created.Value.FestivalId, TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    Assert.Multiple(() =>
                    {
                      Assert.That(hidden.Value, Is.EqualTo(created.Value));
                      Assert.That(_fixture.DbContext.Festivals.Single(festival => festival.Id == created.Value.FestivalId).IsHidden, Is.True);
                    });
  }

  [Test]
  public async Task HideAsync_AFestivalThatIsAlreadyHidden_AnswersWithItAgain()
  {
    var created = await _service.CreateAsync(Request("Herbstfest", _start, _start.AddHours(6)), TestContext.CurrentContext.CancellationToken);
    await _service.HideAsync(created.Value.FestivalId, TestContext.CurrentContext.CancellationToken);

    var hiddenAgain = await _service.HideAsync(created.Value.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(hiddenAgain.Value, Is.EqualTo(created.Value));
  }

  private FestivalService CreateService(GastronomyAppDbContext dbContext)
  {
    RunningFestivalLookup runningFestival = new(new FestivalRepository(dbContext, new()), new(), TimeProvider.System);

    return new(dbContext, new CoreMappingFactory().Create(), new(), runningFestival, new());
  }

  private SaveFestivalRequest Request(string name, DateTime startsAtUtc, DateTime endsAtUtc)
  {
    return new()
           {
             Name = name,
             StartsAtUtc = startsAtUtc,
             EndsAtUtc = endsAtUtc
           };
  }

  private Order BuildOrder(int number, Guid tableId)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             ClientOrderId = Guid.NewGuid(),
             FestivalId = _seeded.FestivalId,
             StaffMemberId = _seeded.StaffMemberId,
             TableId = tableId,
             GlobalOrderNumber = number,
             CreatedAtUtc = DateTime.UtcNow
           };
  }

  private async Task<IReadOnlyList<Guid>> AddIngredientsAsync()
  {
    Unit portion = new()
                   {
                     Id = Guid.NewGuid(),
                     Name = "Portion",
                     IsActive = true
                   };
    List<Ingredient> ingredients =
    [
      new()
      {
        Id = Guid.NewGuid(),
        Name = "Senf",
        UnitId = portion.Id,
        Unit = portion,
        IsActive = true
      },
      new()
      {
        Id = Guid.NewGuid(),
        Name = "Brötchen",
        UnitId = portion.Id,
        Unit = portion,
        IsActive = false
      }
    ];
    _fixture.DbContext.Units.Add(portion);
    _fixture.DbContext.Ingredients.AddRange(ingredients);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return ingredients.Select(ingredient => ingredient.Id).ToList();
  }

  private async Task<Guid> AddAreaAsync(string name, int sortOrder, bool isHidden)
  {
    var areaId = Guid.NewGuid();

    _fixture.DbContext.Areas.Add(new()
                                 {
                                   Id = areaId,
                                   FestivalId = _seeded.FestivalId,
                                   Name = name,
                                   SortOrder = sortOrder,
                                   IsHidden = isHidden
                                 });
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return areaId;
  }

  private async Task AddTableAsync(Guid areaId, string name, bool isHidden)
  {
    _fixture.DbContext.Tables.Add(new()
                                  {
                                    Id = Guid.NewGuid(),
                                    FestivalId = _seeded.FestivalId,
                                    AreaId = areaId,
                                    Name = name,
                                    X = 10.5,
                                    Y = -20.25,
                                    Width = 80,
                                    Height = 40.75,
                                    Rotation = -12.5,
                                    IsHidden = isHidden
                                  });
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }

  private async Task PlaceAsync(Guid stationId, Guid areaId)
  {
    var link = await _fixture.DbContext.FestivalStations.SingleAsync(candidate => candidate.FestivalId == _seeded.FestivalId && candidate.StationId == stationId, TestContext.CurrentContext.CancellationToken);

    _fixture.DbContext.FestivalStationShapes.Add(new()
                                                 {
                                                   FestivalStationId = link.Id,
                                                   FestivalId = _seeded.FestivalId,
                                                   AreaId = areaId,
                                                   X = 100.5,
                                                   Y = 50.25,
                                                   Width = 60,
                                                   Height = 30,
                                                   Rotation = -90
                                                 });
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }
}
