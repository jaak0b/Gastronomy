using GastronomyApp.Contracts.Enums;
using ErrorOr;
using FakeItEasy;
using GastronomyApp.Contracts.Admin.Festivals;
using GastronomyApp.Contracts.Admin.Stations;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace GastronomyApp.Infrastructure.Tests.Services;

[TestFixture]
public sealed class FestivalStationServiceTest
{
  [SetUp]
  public async Task SetUp()
  {
    _fixture = new();
    _seeded = await new DomainSeeder().SeedAsync(_fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    _clock = new(new(_now));
    await MoveTheFestivalAsync(_now.AddHours(-5), _now.AddHours(5));
    _gardenId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Garten", 1, false);
    FestivalRepository festivalRepository = new(_fixture.DbContext, new());
    RunningFestivalLookup runningFestival = new(festivalRepository, new(), _clock);
    _service = new(_fixture.DbContext, new CoreMappingFactory().Create(), new(_fixture.DbContext, _clock), new(_fixture.DbContext), new(_fixture.DbContext, runningFestival), new());
  }

  [TearDown]
  public void TearDown()
  {
    _fixture.Dispose();
  }

  private readonly DateTime _now = new(2026, 8, 27, 18, 0, 0, DateTimeKind.Utc);
  private readonly TablePlanSeeder _plan = new();

  private FakeTimeProvider _clock = null!;
  private SqliteInMemoryFixture _fixture = null!;
  private Guid _gardenId;
  private SeededDomain _seeded = null!;
  private FestivalStationService _service = null!;

  [Test]
  public async Task AddAsync_AFestivalThatDoesNotExist_IsRefusedAsNotFound()
  {
    ErrorOr<SavedStationView> added = await _service.AddAsync(Guid.NewGuid(), _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(added.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
                      Assert.That(added.RefusalMessageKey(), Is.EqualTo("FestivalNotFound"));
                    });
  }

  [Test]
  public async Task AddAsync_AStationThatIsNotThere_FailsBecauseTheStationIsNotFound()
  {
    ErrorOr<SavedStationView> added = await _service.AddAsync(_seeded.FestivalId, Guid.NewGuid(), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(added.IsError, Is.True);
                      Assert.That(added.RefusalMessageKey(), Is.EqualTo("StationNotFound"));
                    });
  }

  [Test]
  public async Task AddAsync_AStationAlreadyAtTheFestival_StoresNoSecondLink()
  {
    ErrorOr<SavedStationView> added = await _service.AddAsync(_seeded.FestivalId, _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    await using var readContext = _fixture.CreateContext();
    var links = await readContext.FestivalStations.Where(link => link.FestivalId == _seeded.FestivalId && link.StationId == _seeded.KitchenStationId).ToListAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(added.Value.StationId, Is.EqualTo(_seeded.KitchenStationId));
                      Assert.That(links, Has.Count.EqualTo(1));
                      Assert.That(links[0].NextStationOrderNumber, Is.EqualTo(1));
                    });
  }

  [Test]
  public async Task AddAsync_AStationComingBackToTheFestival_ContinuesTheNumberingWhereItStopped()
  {
    var tentId = await AddStationAsync("Zelt");
    await AddStationOrderAsync(tentId, 6);

    ErrorOr<SavedStationView> added = await _service.AddAsync(_seeded.FestivalId, tentId, TestContext.CurrentContext.CancellationToken);

    await using var readContext = _fixture.CreateContext();
    var link = await readContext.FestivalStations.SingleAsync(candidate => candidate.FestivalId == _seeded.FestivalId && candidate.StationId == tentId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(added.Value.StationId, Is.EqualTo(tentId));
                      Assert.That(link.NextStationOrderNumber, Is.EqualTo(7));
                    });
  }

  [Test]
  public async Task RemoveAsync_AStationThatIsNotAtTheFestival_FailsBecauseTheLinkIsNotFound()
  {
    ErrorOr<Success> removed = await _service.RemoveAsync(_seeded.FestivalId, Guid.NewGuid(), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(removed.IsError, Is.True);
                      Assert.That(removed.RefusalMessageKey(), Is.EqualTo("StationLinkNotFound"));
                    });
  }

  [Test]
  public async Task RemoveAsync_AStationWithOpenItemsAtTheRunningFestival_KeepsTheStationAtTheFestival()
  {
    await PlaceAnOpenItemAtTheKitchenAsync();

    ErrorOr<Success> removed = await _service.RemoveAsync(_seeded.FestivalId, _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    await using var readContext = _fixture.CreateContext();

    Assert.Multiple(async () =>
                    {
                      Assert.That(removed.IsError, Is.True);
                      Assert.That(removed.RefusalMessageKey(), Is.EqualTo("errors.admin.festivals.stationHasOrdersAtTheFestival"));
                      Assert.That(await readContext.FestivalStations.AnyAsync(link => link.StationId == _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken), Is.True);
                    });
  }

  [Test]
  public async Task RemoveAsync_AStationWithOpenItemsAtAFestivalThatIsOver_RemovesItAnyway()
  {
    await PlaceAnOpenItemAtTheKitchenAsync();
    await MoveTheFestivalAsync(_now.AddHours(-5), _now.AddHours(-1));
    var tentId = await AddStationAsync("Zelt");
    await LinkAsync(tentId);
    await AssignTheSausageToAsync(tentId);

    ErrorOr<Success> removed = await _service.RemoveAsync(_seeded.FestivalId, _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    await using var readContext = _fixture.CreateContext();

    Assert.Multiple(async () =>
                    {
                      Assert.That(removed.IsError, Is.False);
                      Assert.That(await readContext.FestivalStations.AnyAsync(link => link.StationId == _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken), Is.False);
                    });
  }

  [Test]
  public async Task RemoveAsync_AStationTheLastItemNeeds_CountsTheItemsThatWouldBeStranded()
  {
    ErrorOr<Success> removed = await _service.RemoveAsync(_seeded.FestivalId, _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(removed.IsError, Is.True);
                      Assert.That(removed.RefusalMessageKey(), Is.EqualTo("errors.admin.stations.itemsWouldHaveNoStation"));
                      Assert.That(removed.RefusalMetadata("count"), Is.EqualTo("1"));
                    });
  }

  [Test]
  public async Task RemoveAsync_AStationThatSharesItsItemsWithAnother_RemovesTheLinkAndOnlyItsOwnAssignments()
  {
    var tentId = await AddStationAsync("Zelt");
    await LinkAsync(tentId);
    await AssignTheSausageToAsync(tentId);

    ErrorOr<Success> removed = await _service.RemoveAsync(_seeded.FestivalId, tentId, TestContext.CurrentContext.CancellationToken);

    await using var readContext = _fixture.CreateContext();
    var sausageStations = await readContext.ItemStationAssignments.Where(assignment => assignment.CatalogItemId == _seeded.SausageItemId).Select(assignment => assignment.StationId).ToListAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(async () =>
                    {
                      Assert.That(removed.IsError, Is.False);
                      Assert.That(await readContext.FestivalStations.AnyAsync(link => link.StationId == tentId, TestContext.CurrentContext.CancellationToken), Is.False);
                      Assert.That(sausageStations, Is.EqualTo(new[] { _seeded.KitchenStationId }));
                    });
  }

  [Test]
  public async Task RemoveAsync_AStationPlacedOnTheFestivalsPlan_RemovesItsShapeWithIt()
  {
    var tentId = await AddStationAsync("Zelt");
    await LinkAsync(tentId);
    await _plan.PlaceAsync(_fixture.DbContext, _seeded.FestivalId, tentId, _gardenId);

    ErrorOr<Success> removed = await _service.RemoveAsync(_seeded.FestivalId, tentId, TestContext.CurrentContext.CancellationToken);

    await using var readContext = _fixture.CreateContext();

    Assert.Multiple(async () =>
                    {
                      Assert.That(removed.IsError, Is.False);
                      Assert.That(await readContext.FestivalStationShapes.CountAsync(TestContext.CurrentContext.CancellationToken), Is.Zero);
                    });
  }

  [Test]
  public async Task RemoveAsync_AStationNotOnTheFestivalsPlan_RemovesItWithoutTouchingOtherShapes()
  {
    var tentId = await AddStationAsync("Zelt");
    await LinkAsync(tentId);
    await _plan.PlaceAsync(_fixture.DbContext, _seeded.FestivalId, _seeded.BarStationId, _gardenId);

    ErrorOr<Success> removed = await _service.RemoveAsync(_seeded.FestivalId, tentId, TestContext.CurrentContext.CancellationToken);

    await using var readContext = _fixture.CreateContext();

    Assert.Multiple(async () =>
                    {
                      Assert.That(removed.IsError, Is.False);
                      Assert.That(await readContext.FestivalStationShapes.CountAsync(TestContext.CurrentContext.CancellationToken), Is.EqualTo(1));
                    });
  }

  [Test]
  public async Task PlaceShapeAsync_AStationThatDoesNotTakePartInTheFestival_IsNotFound()
  {
    var strangerId = Guid.NewGuid();

    ErrorOr<SavedStationView> placed = await _service.PlaceShapeAsync(_seeded.FestivalId, strangerId, BuildRequest(_gardenId), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(placed.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
                      Assert.That(placed.RefusalMessageKey(), Is.EqualTo("StationLinkNotFound"));
                      Assert.That(placed.RefusalDescription(), Does.Contain(strangerId.ToString()));
                    });
  }

  [Test]
  public async Task PlaceShapeAsync_AnAreaOfAnotherFestival_IsRefusedGenericallyAndStoresNothing()
  {
    var otherFestivalId = await _plan.AddFestivalAsync(_fixture.DbContext);
    var strangerAreaId = await _plan.AddAreaAsync(_fixture.DbContext, otherFestivalId, "Halle", 1, false);

    ErrorOr<SavedStationView> placed = await _service.PlaceShapeAsync(_seeded.FestivalId, _seeded.KitchenStationId, BuildRequest(strangerAreaId), TestContext.CurrentContext.CancellationToken);

    await using var readContext = _fixture.CreateContext();

    Assert.Multiple(async () =>
                    {
                      Assert.That(placed.FirstError.NumericType, Is.EqualTo(RefusalType.BadRequest));
                      Assert.That(placed.RefusalMessageKey(), Is.EqualTo("errors.admin.actionFailed"));
                      Assert.That(placed.RefusalDescription(), Does.Contain(strangerAreaId.ToString()));
                      Assert.That(await readContext.FestivalStationShapes.CountAsync(TestContext.CurrentContext.CancellationToken), Is.Zero);
                    });
  }

  [Test]
  public async Task PlaceShapeAsync_AStationNotOnThePlanYet_StoresItsShapeExactlyAsSent()
  {
    ErrorOr<SavedStationView> placed = await _service.PlaceShapeAsync(_seeded.FestivalId,
                                                                      _seeded.KitchenStationId,
                                                                      new()
                                                                      {
                                                                        AreaId = _gardenId,
                                                                        Geometry = new()
                                                                                   {
                                                                                     X = -3.75,
                                                                                     Y = 412.125,
                                                                                     Width = 55.5,
                                                                                     Height = 0.25,
                                                                                     Rotation = -90.5
                                                                                   }
                                                                      },
                                                                      TestContext.CurrentContext.CancellationToken);

    await using var readContext = _fixture.CreateContext();
    var link = await readContext.FestivalStations.SingleAsync(candidate => candidate.StationId == _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);
    var shape = await readContext.FestivalStationShapes.SingleAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(placed.Value.StationId, Is.EqualTo(_seeded.KitchenStationId));
                      Assert.That(shape.FestivalStationId, Is.EqualTo(link.Id));
                      Assert.That(shape.FestivalId, Is.EqualTo(_seeded.FestivalId));
                      Assert.That(shape.AreaId, Is.EqualTo(_gardenId));
                      Assert.That(shape.X, Is.EqualTo(-3.75));
                      Assert.That(shape.Y, Is.EqualTo(412.125));
                      Assert.That(shape.Width, Is.EqualTo(55.5));
                      Assert.That(shape.Height, Is.EqualTo(0.25));
                      Assert.That(shape.Rotation, Is.EqualTo(-90.5));
                    });
  }

  [Test]
  public async Task PlaceShapeAsync_AStationAlreadyOnThePlan_MovesTheSameShapeToTheNewArea()
  {
    var hallId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Halle", 2, false);
    await _plan.PlaceAsync(_fixture.DbContext, _seeded.FestivalId, _seeded.KitchenStationId, _gardenId);
    var shapeId = (await _fixture.DbContext.FestivalStationShapes.SingleAsync(TestContext.CurrentContext.CancellationToken)).FestivalStationId;
    _fixture.DbContext.ChangeTracker.Clear();

    ErrorOr<SavedStationView> placed = await _service.PlaceShapeAsync(_seeded.FestivalId, _seeded.KitchenStationId, BuildRequest(hallId), TestContext.CurrentContext.CancellationToken);

    await using var readContext = _fixture.CreateContext();
    var shape = await readContext.FestivalStationShapes.SingleAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(placed.IsError, Is.False);
                      Assert.That(shape.FestivalStationId, Is.EqualTo(shapeId));
                      Assert.That(shape.AreaId, Is.EqualTo(hallId));
                      Assert.That(shape.X, Is.EqualTo(150));
                      Assert.That(shape.Y, Is.EqualTo(75));
                      Assert.That(shape.Width, Is.EqualTo(60));
                      Assert.That(shape.Height, Is.EqualTo(30));
                      Assert.That(shape.Rotation, Is.EqualTo(15));
                    });
  }

  [Test]
  public async Task RemoveShapeAsync_AStationNotOnThePlan_IsNotFound()
  {
    ErrorOr<Success> removed = await _service.RemoveShapeAsync(_seeded.FestivalId, _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(removed.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
                      Assert.That(removed.RefusalMessageKey(), Is.EqualTo("StationShapeNotFound"));
                    });
  }

  [Test]
  public async Task RemoveShapeAsync_AStationThatDoesNotTakePartInTheFestival_IsNotFound()
  {
    ErrorOr<Success> removed = await _service.RemoveShapeAsync(_seeded.FestivalId, Guid.NewGuid(), TestContext.CurrentContext.CancellationToken);

    Assert.That(removed.RefusalMessageKey(), Is.EqualTo("StationLinkNotFound"));
  }

  [Test]
  public async Task RemoveShapeAsync_AStationOnThePlan_RemovesItsShapeAndKeepsTheStationAtTheFestival()
  {
    await _plan.PlaceAsync(_fixture.DbContext, _seeded.FestivalId, _seeded.KitchenStationId, _gardenId);

    ErrorOr<Success> removed = await _service.RemoveShapeAsync(_seeded.FestivalId, _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    await using var readContext = _fixture.CreateContext();

    Assert.Multiple(async () =>
                    {
                      Assert.That(removed.IsError, Is.False);
                      Assert.That(await readContext.FestivalStationShapes.CountAsync(TestContext.CurrentContext.CancellationToken), Is.Zero);
                      Assert.That(await readContext.FestivalStations.AnyAsync(link => link.StationId == _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken), Is.True);
                    });
  }

  private static SaveStationShapeRequest BuildRequest(Guid areaId)
  {
    return new()
           {
             AreaId = areaId,
             Geometry = new()
                        {
                          X = 150,
                          Y = 75,
                          Width = 60,
                          Height = 30,
                          Rotation = 15
                        }
           };
  }

  private async Task PlaceAnOpenItemAtTheKitchenAsync()
  {
    var tableId = await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, _gardenId, "Tisch 1", false);
    await _plan.AddOrderAsync(_fixture.DbContext, _seeded, tableId, 1, null, null);
  }

  private async Task AddStationOrderAsync(Guid stationId, int stationOrderNumber)
  {
    var orderId = Guid.NewGuid();
    var tableId = await new DomainSeeder().SeedTableAsync(_fixture.DbContext, _seeded.FestivalId, $"Tisch {stationOrderNumber}", TestContext.CurrentContext.CancellationToken);

    _fixture.DbContext.Orders.Add(new()
                                  {
                                    Id = orderId,
                                    ClientOrderId = Guid.NewGuid(),
                                    FestivalId = _seeded.FestivalId,
                                    GlobalOrderNumber = stationOrderNumber,
                                    StaffMemberId = _seeded.StaffMemberId,
                                    TableId = tableId,
                                    CreatedAtUtc = _now
                                  });
    _fixture.DbContext.StationOrders.Add(new()
                                         {
                                           Id = Guid.NewGuid(),
                                           OrderId = orderId,
                                           FestivalId = _seeded.FestivalId,
                                           StationId = stationId,
                                           StationOrderNumber = stationOrderNumber,
                                           DeliveryMode = DeliveryMode.Together
                                         });
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }

  private async Task<Guid> AddStationAsync(string name)
  {
    Station station = new()
                      {
                        Id = Guid.NewGuid(),
                        Name = name,
                        SortOrder = 3,
                        IsActive = true
                      };
    _fixture.DbContext.Stations.Add(station);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return station.Id;
  }

  private async Task LinkAsync(Guid stationId)
  {
    _fixture.DbContext.FestivalStations.Add(new()
                                            {
                                              Id = Guid.NewGuid(),
                                              FestivalId = _seeded.FestivalId,
                                              StationId = stationId,
                                              NextStationOrderNumber = 1
                                            });
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }

  private async Task AssignTheSausageToAsync(Guid stationId)
  {
    _fixture.DbContext.ItemStationAssignments.Add(new()
                                                  {
                                                    Id = Guid.NewGuid(),
                                                    FestivalId = _seeded.FestivalId,
                                                    CatalogItemId = _seeded.SausageItemId,
                                                    StationId = stationId
                                                  });
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }

  private async Task MoveTheFestivalAsync(DateTime startsAtUtc, DateTime endsAtUtc)
  {
    var festival = await _fixture.DbContext.Festivals.SingleAsync(row => row.Id == _seeded.FestivalId, TestContext.CurrentContext.CancellationToken);
    festival.StartsAtUtc = startsAtUtc;
    festival.EndsAtUtc = endsAtUtc;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }
}
