using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Persistence;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace GastronomyApp.Infrastructure.Tests.Services;

[TestFixture]
public sealed class TablePlanServiceTest
{
  [SetUp]
  public async Task SetUp()
  {
    _fixture = new();
    _seeded = await new DomainSeeder().SeedAsync(_fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    _clock = new(new(_now));
    await MoveTheFestivalAsync(_now.AddHours(-5), _now.AddHours(5));
    _service = CreateService(_fixture.DbContext);
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
  private SeededDomain _seeded = null!;
  private TablePlanService _service = null!;

  [Test]
  public async Task ListAsync_AFestivalThatIsNotThere_IsNotFound()
  {
    var plan = await _service.ListAsync(Guid.NewGuid(), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(plan.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
                      Assert.That(plan.RefusalMessageKey(), Is.EqualTo("FestivalNotFound"));
                    });
  }

  [Test]
  public async Task ListAsync_AFestivalWithAreasTablesAndPlacedStations_AnswersEveryAreaInSortOrderWithItsContents()
  {
    var hallId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Halle", 2, true);
    var gardenId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Garten", 1, false);
    await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, gardenId, "Tisch 2", false);
    var hiddenTableId = await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, gardenId, "Tisch 1", true);
    await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, hallId, "Stammtisch", false);
    await _plan.PlaceAsync(_fixture.DbContext, _seeded.FestivalId, _seeded.BarStationId, gardenId);
    await _plan.PlaceAsync(_fixture.DbContext, _seeded.FestivalId, _seeded.KitchenStationId, gardenId);
    var otherFestivalId = await _plan.AddFestivalAsync(_fixture.DbContext);
    await _plan.AddAreaAsync(_fixture.DbContext, otherFestivalId, "Garten", 0, false);
    _fixture.DbContext.ChangeTracker.Clear();

    var plan = (await _service.ListAsync(_seeded.FestivalId, TestContext.CurrentContext.CancellationToken)).Value;

    Assert.Multiple(() =>
                    {
                      Assert.That(plan.Areas.Select(area => area.Name),
                                  Is.EqualTo(new[]
                                             {
                                               "Garten",
                                               "Halle"
                                             }));
                      Assert.That(plan.Areas[0].IsHidden, Is.False);
                      Assert.That(plan.Areas[0].Tables.Select(table => table.Name),
                                  Is.EqualTo(new[]
                                             {
                                               "Tisch 1",
                                               "Tisch 2"
                                             }));
                      Assert.That(plan.Areas[0].Tables[0].TableId, Is.EqualTo(hiddenTableId));
                      Assert.That(plan.Areas[0].Tables[0].IsHidden, Is.True);
                      Assert.That(plan.Areas[0].Tables[1].Geometry.X, Is.EqualTo(10));
                      Assert.That(plan.Areas[0].Tables[1].Geometry.Width, Is.EqualTo(80));
                      Assert.That(plan.Areas[0].StationShapes.Select(shape => shape.StationName),
                                  Is.EqualTo(new[]
                                             {
                                               "Kueche",
                                               "Theke"
                                             }));
                      Assert.That(plan.Areas[0].StationShapes.Select(shape => shape.StationId),
                                  Is.EqualTo(new[]
                                             {
                                               _seeded.KitchenStationId,
                                               _seeded.BarStationId
                                             }));
                      Assert.That(plan.Areas[0].StationShapes.Select(shape => shape.Geometry.Width), Is.All.EqualTo(60));
                      Assert.That(plan.Areas[1].IsHidden, Is.True);
                      Assert.That(plan.Areas[1].Tables.Select(table => table.Name), Is.EqualTo(new[] { "Stammtisch" }));
                      Assert.That(plan.Areas[1].StationShapes, Is.Empty);
                    });
  }

  [Test]
  public async Task ListAsync_StationsOfEqualPositionInMixedCase_AreListedWithCapitalsIgnored()
  {
    var gardenId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Garten", 1, false);
    var kitchen = await _fixture.DbContext.Stations.SingleAsync(station => station.Id == _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);
    var bar = await _fixture.DbContext.Stations.SingleAsync(station => station.Id == _seeded.BarStationId, TestContext.CurrentContext.CancellationToken);
    kitchen.Name = "Grill";
    bar.Name = "bar";
    bar.SortOrder = kitchen.SortOrder;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    await _plan.PlaceAsync(_fixture.DbContext, _seeded.FestivalId, _seeded.KitchenStationId, gardenId);
    await _plan.PlaceAsync(_fixture.DbContext, _seeded.FestivalId, _seeded.BarStationId, gardenId);
    _fixture.DbContext.ChangeTracker.Clear();

    var plan = (await _service.ListAsync(_seeded.FestivalId, TestContext.CurrentContext.CancellationToken)).Value;

    Assert.That(plan.Areas[0].StationShapes.Select(shape => shape.StationName),
                Is.EqualTo(new[]
                           {
                             "bar",
                             "Grill"
                           }));
  }

  [Test]
  public async Task ListAsync_OneTableWithAnItemNotYetSettledWhileTheFestivalRuns_SaysSoForThatTableAndItsAreaOnly()
  {
    var gardenId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Garten", 1, false);
    var hallId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Halle", 2, false);
    var unsettledTableId = await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, gardenId, "Tisch 1", false);
    var finishedTableId = await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, gardenId, "Tisch 2", false);
    var hallTableId = await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, hallId, "Tisch 3", false);
    await _plan.AddOrderAsync(_fixture.DbContext, _seeded, unsettledTableId, 1, _now, null);
    await _plan.AddOrderAsync(_fixture.DbContext, _seeded, finishedTableId, 2, _now, _now);
    await _plan.AddOrderAsync(_fixture.DbContext, _seeded, hallTableId, 3, _now, _now);
    _fixture.DbContext.ChangeTracker.Clear();

    var plan = (await _service.ListAsync(_seeded.FestivalId, TestContext.CurrentContext.CancellationToken)).Value;

    Assert.Multiple(() =>
                    {
                      Assert.That(plan.Areas.Select(area => area.HasItemsToHandOutOrSettleAtTheRunningFestival),
                                  Is.EqualTo(new[]
                                             {
                                               true,
                                               false
                                             }));
                      Assert.That(plan.Areas[0].Tables.Select(table => table.HasItemsToHandOutOrSettleAtTheRunningFestival),
                                  Is.EqualTo(new[]
                                             {
                                               true,
                                               false
                                             }));
                      Assert.That(plan.Areas[1].Tables.Single().HasItemsToHandOutOrSettleAtTheRunningFestival, Is.False);
                    });
  }

  [Test]
  public async Task ListAsync_AnItemNotYetSettledAtAnotherFestival_LeavesThisFestivalsAreasAndTablesUnflagged()
  {
    var gardenId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Garten", 1, false);
    await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, gardenId, "Tisch 1", false);
    var otherFestivalId = await _plan.AddFestivalAsync(_fixture.DbContext);
    var otherAreaId = await _plan.AddAreaAsync(_fixture.DbContext, otherFestivalId, "Wiese", 1, false);
    var otherTableId = await _plan.AddTableAsync(_fixture.DbContext, otherFestivalId, otherAreaId, "Tisch 1", false);
    await _plan.AddOrderAsync(_fixture.DbContext, _seeded, otherTableId, 1, _now, null, otherFestivalId);
    _fixture.DbContext.ChangeTracker.Clear();

    var plan = (await _service.ListAsync(_seeded.FestivalId, TestContext.CurrentContext.CancellationToken)).Value;

    Assert.Multiple(() =>
                    {
                      Assert.That(plan.Areas.Single().HasItemsToHandOutOrSettleAtTheRunningFestival, Is.False);
                      Assert.That(plan.Areas.Single().Tables.Single().HasItemsToHandOutOrSettleAtTheRunningFestival, Is.False);
                    });
  }

  [Test]
  public async Task ListAsync_ATableWithAnItemNotYetSettledAfterTheFestival_SaysNoTableOrAreaHasItemsLeft()
  {
    var gardenId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Garten", 1, false);
    var tableId = await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, gardenId, "Tisch 1", false);
    await _plan.AddOrderAsync(_fixture.DbContext, _seeded, tableId, 1, _now, null);
    await MoveTheFestivalAsync(_now.AddHours(-6), _now.AddHours(-1));
    _fixture.DbContext.ChangeTracker.Clear();

    var plan = (await _service.ListAsync(_seeded.FestivalId, TestContext.CurrentContext.CancellationToken)).Value;

    Assert.Multiple(() =>
                    {
                      Assert.That(plan.Areas.Single().HasItemsToHandOutOrSettleAtTheRunningFestival, Is.False);
                      Assert.That(plan.Areas.Single().Tables.Single().HasItemsToHandOutOrSettleAtTheRunningFestival, Is.False);
                    });
  }

  [Test]
  public async Task ReadRunningFestivalAsync_NoFestivalIsRunning_AnswersNoArea()
  {
    await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Garten", 1, false);
    await MoveTheFestivalAsync(_now.AddHours(-6), _now.AddHours(-1));

    var plan = await _service.ReadRunningFestivalAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(plan.Areas, Is.Empty);
  }

  [Test]
  public async Task ReadRunningFestivalAsync_HiddenAreasAndTablesBesideShownOnes_AnswersTheShownOnesWithTheirStations()
  {
    var gardenId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Garten", 1, false);
    var hallId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Halle", 2, true);
    var terraceId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Terrasse", 3, false);
    await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, gardenId, "Tisch 2", false);
    await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, gardenId, "Tisch 3", true);
    await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, gardenId, "Tisch 1", false);
    await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, hallId, "Stammtisch", false);
    await _plan.PlaceAsync(_fixture.DbContext, _seeded.FestivalId, _seeded.BarStationId, gardenId);
    await _plan.PlaceAsync(_fixture.DbContext, _seeded.FestivalId, _seeded.KitchenStationId, gardenId);
    var otherFestivalId = await _plan.AddFestivalAsync(_fixture.DbContext);
    await _plan.AddAreaAsync(_fixture.DbContext, otherFestivalId, "Wiese", 0, false);
    _fixture.DbContext.ChangeTracker.Clear();

    var plan = await _service.ReadRunningFestivalAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(plan.Areas.Select(area => area.Name),
                                  Is.EqualTo(new[]
                                             {
                                               "Garten",
                                               "Terrasse"
                                             }));
                      Assert.That(plan.Areas[0].AreaId, Is.EqualTo(gardenId));
                      Assert.That(plan.Areas[1].AreaId, Is.EqualTo(terraceId));
                      Assert.That(plan.Areas[0].Tables.Select(table => table.Name),
                                  Is.EqualTo(new[]
                                             {
                                               "Tisch 1",
                                               "Tisch 2"
                                             }));
                      Assert.That(plan.Areas[0].Tables[0].Geometry.Width, Is.EqualTo(80));
                      Assert.That(plan.Areas[0].StationShapes.Select(shape => shape.StationName),
                                  Is.EqualTo(new[]
                                             {
                                               "Kueche",
                                               "Theke"
                                             }));
                      Assert.That(plan.Areas[0].StationShapes.Select(shape => shape.Geometry.Width), Is.All.EqualTo(60));
                      Assert.That(plan.Areas[1].Tables, Is.Empty);
                      Assert.That(plan.Areas[1].StationShapes, Is.Empty);
                    });
  }

  [Test]
  public async Task ReadRunningFestivalAsync_HiddenAreaAndHiddenTableHoldingUnfinishedOrders_AreNotShownAndDoNotChangeTheShownTables()
  {
    var gardenId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Garten", 1, false);
    var hallId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Halle", 2, true);
    var shownTableId = await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, gardenId, "Tisch 1", false);
    var hiddenTableId = await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, gardenId, "Tisch 2", true);
    var tableInHiddenAreaId = await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, hallId, "Stammtisch", false);
    await _plan.AddOrderAsync(_fixture.DbContext, _seeded, shownTableId, 1, _now, _now);
    await _plan.AddOrderAsync(_fixture.DbContext, _seeded, hiddenTableId, 2, null, null);
    await _plan.AddOrderAsync(_fixture.DbContext, _seeded, tableInHiddenAreaId, 3, null, null);
    _fixture.DbContext.ChangeTracker.Clear();

    var plan = await _service.ReadRunningFestivalAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(plan.Areas.Select(area => area.AreaId), Is.EqualTo(new[] { gardenId }));
                      Assert.That(plan.Areas.Single().Tables.Select(table => table.TableId), Is.EqualTo(new[] { shownTableId }));
                      Assert.That(plan.Areas.Single().Tables.Single().HandOutProgress, Is.EqualTo(ShareState.All));
                      Assert.That(plan.Areas.Single().Tables.Single().SettlementProgress, Is.EqualTo(ShareState.All));
                    });
  }

  [Test]
  public async Task ListAsync_ATableWhoseOnlyItemNotYetSettledWasCancelled_SaysNoTableOrAreaHasItemsLeft()
  {
    var gardenId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Garten", 1, false);
    var tableId = await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, gardenId, "Tisch 1", false);
    await _plan.AddOrderAsync(_fixture.DbContext, _seeded, tableId, 1, null, null);
    await _plan.CancelEveryItemAtTableAsync(_fixture.DbContext, _seeded, tableId);
    _fixture.DbContext.ChangeTracker.Clear();

    var plan = (await _service.ListAsync(_seeded.FestivalId, TestContext.CurrentContext.CancellationToken)).Value;

    Assert.Multiple(() =>
                    {
                      Assert.That(plan.Areas.Single().HasItemsToHandOutOrSettleAtTheRunningFestival, Is.False);
                      Assert.That(plan.Areas.Single().Tables.Single().HasItemsToHandOutOrSettleAtTheRunningFestival, Is.False);
                    });
  }

  [Test]
  public async Task ReadRunningFestivalAsync_ATableWhoseOnlyOrderWasCancelledEntirely_CountsItAsFinished()
  {
    var gardenId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Garten", 1, false);
    var cancelledId = await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, gardenId, "Tisch 1", false);
    var untouchedId = await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, gardenId, "Tisch 2", false);
    await _plan.AddOrderAsync(_fixture.DbContext, _seeded, cancelledId, 1, null, null);
    await _plan.AddOrderAsync(_fixture.DbContext, _seeded, untouchedId, 2, null, null);
    await _plan.CancelEveryItemAtTableAsync(_fixture.DbContext, _seeded, cancelledId);
    _fixture.DbContext.ChangeTracker.Clear();

    var plan = await _service.ReadRunningFestivalAsync(TestContext.CurrentContext.CancellationToken);
    var tables = plan.Areas.Single().Tables.ToDictionary(table => table.TableId);

    Assert.Multiple(() =>
                    {
                      Assert.That(tables[cancelledId].HandOutProgress, Is.EqualTo(ShareState.All));
                      Assert.That(tables[cancelledId].SettlementProgress, Is.EqualTo(ShareState.All));
                      Assert.That(tables[untouchedId].HandOutProgress, Is.EqualTo(ShareState.None));
                    });
  }

  [Test]
  public async Task ReadRunningFestivalAsync_ATableWithOrders_AnswersTheProgressOfItsUnfinishedOrders()
  {
    var gardenId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Garten", 1, false);
    var orderedAtId = await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, gardenId, "Tisch 1", false);
    var finishedId = await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, gardenId, "Tisch 2", false);
    var untouchedId = await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, gardenId, "Tisch 3", false);
    var emptyId = await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, gardenId, "Tisch 4", false);
    await _plan.AddOrderAsync(_fixture.DbContext, _seeded, orderedAtId, 1, _now, _now);
    await _plan.AddOrderAsync(_fixture.DbContext, _seeded, orderedAtId, 2, _now, null);
    await _plan.AddOrderAsync(_fixture.DbContext, _seeded, finishedId, 3, _now, _now);
    await _plan.AddOrderAsync(_fixture.DbContext, _seeded, untouchedId, 4, null, null);
    _fixture.DbContext.ChangeTracker.Clear();

    var plan = await _service.ReadRunningFestivalAsync(TestContext.CurrentContext.CancellationToken);
    var tables = plan.Areas.Single().Tables.ToDictionary(table => table.TableId);

    Assert.Multiple(() =>
                    {
                      Assert.That(tables[orderedAtId].HandOutProgress, Is.EqualTo(ShareState.All));
                      Assert.That(tables[orderedAtId].SettlementProgress, Is.EqualTo(ShareState.None));
                      Assert.That(tables[finishedId].HandOutProgress, Is.EqualTo(ShareState.All));
                      Assert.That(tables[finishedId].SettlementProgress, Is.EqualTo(ShareState.All));
                      Assert.That(tables[untouchedId].HandOutProgress, Is.EqualTo(ShareState.None));
                      Assert.That(tables[untouchedId].SettlementProgress, Is.EqualTo(ShareState.None));
                      Assert.That(tables[emptyId].HandOutProgress, Is.EqualTo(ShareState.All));
                      Assert.That(tables[emptyId].SettlementProgress, Is.EqualTo(ShareState.All));
                    });
  }

  [Test]
  public async Task ReadRunningFestivalAsync_OrdersAtTwoStationsBesideTwoStationShapesInOneArea_AnswersEveryTableAndShapeOnceWithTheSharedProgress()
  {
    var gardenId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Garten", 1, false);
    var splitOrderTableId = await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, gardenId, "Tisch 1", false);
    var finishedTableId = await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, gardenId, "Tisch 2", false);
    await _plan.PlaceAsync(_fixture.DbContext, _seeded.FestivalId, _seeded.KitchenStationId, gardenId);
    await _plan.PlaceAsync(_fixture.DbContext, _seeded.FestivalId, _seeded.BarStationId, gardenId);
    await _plan.AddOrderAtKitchenAndBarAsync(_fixture.DbContext, _seeded, splitOrderTableId, 1, _now);
    await _plan.AddOrderAsync(_fixture.DbContext, _seeded, splitOrderTableId, 2, null, null);
    await _plan.AddOrderAsync(_fixture.DbContext, _seeded, finishedTableId, 3, _now, _now);
    _fixture.DbContext.ChangeTracker.Clear();

    var plan = await _service.ReadRunningFestivalAsync(TestContext.CurrentContext.CancellationToken);
    var garden = plan.Areas.Single();

    Assert.Multiple(() =>
                    {
                      Assert.That(garden.StationShapes.Select(shape => shape.StationName),
                                  Is.EqualTo(new[]
                                             {
                                               "Kueche",
                                               "Theke"
                                             }));
                      Assert.That(garden.Tables.Select(table => table.Name),
                                  Is.EqualTo(new[]
                                             {
                                               "Tisch 1",
                                               "Tisch 2"
                                             }));
                      Assert.That(garden.Tables[0].HandOutProgress, Is.EqualTo(ShareState.Some));
                      Assert.That(garden.Tables[0].SettlementProgress, Is.EqualTo(ShareState.None));
                      Assert.That(garden.Tables[1].HandOutProgress, Is.EqualTo(ShareState.All));
                      Assert.That(garden.Tables[1].SettlementProgress, Is.EqualTo(ShareState.All));
                    });
  }

  [Test]
  public async Task ReadRunningFestivalAsync_AnUntouchedOrderBesideAHandedOutAndSettledOne_IsNoneOnBothSides()
  {
    var gardenId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Garten", 1, false);
    var tableId = await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, gardenId, "Tisch 1", false);
    await _plan.AddOrderAsync(_fixture.DbContext, _seeded, tableId, 1, _now, _now);
    await _plan.AddOrderAsync(_fixture.DbContext, _seeded, tableId, 2, null, null);
    _fixture.DbContext.ChangeTracker.Clear();

    var plan = await _service.ReadRunningFestivalAsync(TestContext.CurrentContext.CancellationToken);
    var table = plan.Areas.Single().Tables.Single();

    Assert.Multiple(() =>
                    {
                      Assert.That(table.HandOutProgress, Is.EqualTo(ShareState.None));
                      Assert.That(table.SettlementProgress, Is.EqualTo(ShareState.None));
                    });
  }

  [Test]
  public async Task ReadRunningFestivalAsync_EverythingSettledButNothingHandedOut_IsNoneForHandingOutAndAllForSettling()
  {
    var gardenId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Garten", 1, false);
    var tableId = await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, gardenId, "Tisch 1", false);
    await _plan.AddOrderAsync(_fixture.DbContext, _seeded, tableId, 1, null, _now);
    _fixture.DbContext.ChangeTracker.Clear();

    var plan = await _service.ReadRunningFestivalAsync(TestContext.CurrentContext.CancellationToken);
    var table = plan.Areas.Single().Tables.Single();

    Assert.Multiple(() =>
                    {
                      Assert.That(table.HandOutProgress, Is.EqualTo(ShareState.None));
                      Assert.That(table.SettlementProgress, Is.EqualTo(ShareState.All));
                    });
  }

  [Test]
  public async Task ReadRunningFestivalAsync_AnOrderWithOneOfItsTwoItemsSettled_IsSomeForSettling()
  {
    var gardenId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Garten", 1, false);
    var tableId = await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, gardenId, "Tisch 1", false);
    await _plan.AddOrderAtKitchenAndBarAsync(_fixture.DbContext, _seeded, tableId, 1, null);
    var sausage = await _fixture.DbContext.OrderItems.SingleAsync(item => item.ItemName == "Bratwurst", TestContext.CurrentContext.CancellationToken);
    sausage.SettledAtUtc = _now;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var plan = await _service.ReadRunningFestivalAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(plan.Areas.Single().Tables.Single().SettlementProgress, Is.EqualTo(ShareState.Some));
  }

  [Test]
  public async Task ReadRunningFestivalAsync_APlanWithOrdersAtTwoStationsAndStationShapes_ReadsThePlanInOneStatement()
  {
    var gardenId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Garten", 1, false);
    var hallId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Halle", 2, false);
    var tableId = await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, gardenId, "Tisch 1", false);
    await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, hallId, "Tisch 2", false);
    await _plan.PlaceAsync(_fixture.DbContext, _seeded.FestivalId, _seeded.KitchenStationId, gardenId);
    await _plan.PlaceAsync(_fixture.DbContext, _seeded.FestivalId, _seeded.BarStationId, hallId);
    await _plan.AddOrderAtKitchenAndBarAsync(_fixture.DbContext, _seeded, tableId, 1, null);
    QueryCounter counter = new();
    await using GastronomyAppDbContext readContext = new(new DbContextOptionsBuilder<GastronomyAppDbContext>().UseSqlite(_fixture.Connection).AddInterceptors(counter).Options);

    await CreateService(readContext).ReadRunningFestivalAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(counter.QueryCount, Is.EqualTo(1));
  }

  private TablePlanService CreateService(GastronomyAppDbContext database)
  {
    RunningFestivalLookup runningFestival = new(_fixture.DbContext, new(), _clock);

    return new(database, new CoreMappingFactory().Create(), runningFestival);
  }

  private async Task MoveTheFestivalAsync(DateTime startsAtUtc, DateTime endsAtUtc)
  {
    var festival = await _fixture.DbContext.Festivals.SingleAsync(row => row.Id == _seeded.FestivalId, TestContext.CurrentContext.CancellationToken);
    festival.StartsAtUtc = startsAtUtc;
    festival.EndsAtUtc = endsAtUtc;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }
}
