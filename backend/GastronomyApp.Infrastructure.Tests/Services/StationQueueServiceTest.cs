using ErrorOr;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Contracts.Stations;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Persistence;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace GastronomyApp.Infrastructure.Tests.Services;

[TestFixture]
public sealed class StationQueueServiceTest
{
  [SetUp]
  public async Task SetUp()
  {
    _fixture = new();
    _seeded = await new DomainSeeder().SeedAsync(_fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    _clock = new(DateTimeOffset.UtcNow);
    _service = CreateService(_fixture.DbContext);
  }

  [TearDown]
  public void TearDown()
  {
    _fixture.Dispose();
  }

  private readonly DateTime _orderedAtUtc = new(2026, 8, 27, 18, 30, 0, DateTimeKind.Utc);

  private FakeTimeProvider _clock = null!;
  private SqliteInMemoryFixture _fixture = null!;
  private SeededDomain _seeded = null!;
  private StationQueueService _service = null!;

  [Test]
  public async Task ReadQueueAsync_TheStationHasOpenOrders_NamesTheStationAndCarriesThemInTheOrderTheStationNumbersThem()
  {
    await PlaceOrderAsync(_seeded.KitchenStationId, "Tisch 12", 2, DeliveryMode.AsItComes);
    await PlaceOrderAsync(_seeded.KitchenStationId, "Tisch 3", 1, DeliveryMode.Together);

    ErrorOr<StationQueueView> queue = await _service.ReadQueueAsync(_seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(queue.IsSuccess, Is.True);
                      Assert.That(queue.Value.Station.Id, Is.EqualTo(_seeded.KitchenStationId));
                      Assert.That(queue.Value.Station.Name, Is.EqualTo("Kueche"));
                      Assert.That(queue.Value.Orders.Select(stationOrder => stationOrder.StationOrderNumber),
                                  Is.EqualTo(new[]
                                             {
                                               1,
                                               2
                                             }));
                      Assert.That(queue.Value.Orders.Select(stationOrder => stationOrder.TableName),
                                  Is.EqualTo(new[]
                                             {
                                               "Tisch 3",
                                               "Tisch 12"
                                             }));
                    });
  }

  [Test]
  public async Task ReadQueueAsync_AStationOrderWithOpenItems_CarriesTheTableTheWaiterAndBothCounts()
  {
    IReadOnlyList<Guid> items = await PlaceOrderAsync(_seeded.KitchenStationId, "Tisch 12", 7, DeliveryMode.Together);
    await StampAsync([items[1]]);

    ErrorOr<StationQueueView> queue = await _service.ReadQueueAsync(_seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    var order = queue.Value.Orders.Single();

    Assert.Multiple(() =>
                    {
                      Assert.That(order.GlobalOrderNumber, Is.EqualTo(7));
                      Assert.That(order.StationOrderNumber, Is.EqualTo(7));
                      Assert.That(order.TableName, Is.EqualTo("Tisch 12"));
                      Assert.That(order.StaffMemberName, Is.EqualTo("Anna"));
                      Assert.That(order.CreatedAtUtc, Is.EqualTo(_orderedAtUtc));
                      Assert.That(order.DeliveryMode, Is.EqualTo(DeliveryMode.Together));
                      Assert.That(order.ItemCount, Is.EqualTo(2));
                      Assert.That(order.FulfilledItemCount, Is.EqualTo(1));
                    });
  }

  [Test]
  public async Task ReadQueueAsync_AnItemNoteTheWaiterTyped_TravelsWithTheLine()
  {
    await PlaceOrderAsync(_seeded.KitchenStationId, "Tisch 12", 1, DeliveryMode.Together);

    ErrorOr<StationQueueView> queue = await _service.ReadQueueAsync(_seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    var bratwurst = queue.Value.Orders.Single().Items.Single(item => item.ItemName == "Bratwurst");

    Assert.That(bratwurst.Note, Is.EqualTo("Ohne Ketchup"));
  }

  [Test]
  public async Task ReadQueueAsync_ItemsOfOneStationOrder_ComeBackTogetherByNameAndNoteWithCapitalsIgnoredAndUmlautsAfterZ()
  {
    var stationOrderId = await PlaceBareStationOrderAsync(_seeded.KitchenStationId, "Tisch 12", 1, DeliveryMode.Together);
    await AddItemsAsync(stationOrderId, Line("Zwiebel", null), Line("Äpfel", null), Line("apfel", null), Line("Käsekrainer", "Ohne Ketchup"), Line("Käsekrainer", null), Line("Birne", null));

    ErrorOr<StationQueueView> queue = await _service.ReadQueueAsync(_seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    var items = queue.Value.Orders.Single().Items;

    Assert.Multiple(() =>
                    {
                      Assert.That(items.Select(item => item.ItemName),
                                  Is.EqualTo(new[]
                                             {
                                               "apfel",
                                               "Birne",
                                               "Käsekrainer",
                                               "Käsekrainer",
                                               "Zwiebel",
                                               "Äpfel"
                                             }));
                      Assert.That(items.Where(item => item.ItemName == "Käsekrainer").Select(item => item.Note),
                                  Is.EqualTo(new string?[]
                                             {
                                               null,
                                               "Ohne Ketchup"
                                             }));
                    });
  }

  [Test]
  public async Task ReadQueueAsync_AnAsItComesOrder_AlsoStandsInTheSecondColumn()
  {
    await PlaceOrderAsync(_seeded.KitchenStationId, "Tisch 12", 1, DeliveryMode.Together);
    await PlaceOrderAsync(_seeded.KitchenStationId, "Tisch 3", 2, DeliveryMode.AsItComes);

    ErrorOr<StationQueueView> queue = await _service.ReadQueueAsync(_seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(queue.Value.Orders, Has.Count.EqualTo(2));
                      Assert.That(queue.Value.AsItComes.Select(stationOrder => stationOrder.StationOrderNumber), Is.EqualTo(new[] { 2 }));
                    });
  }

  [Test]
  public async Task ReadQueueAsync_AnAsItComesOrderTheEmployeeHid_LeavesItOutOfTheSecondColumnOnly()
  {
    await PlaceOrderAsync(_seeded.KitchenStationId, "Tisch 12", 1, DeliveryMode.AsItComes);
    var stationOrder = await _fixture.DbContext.StationOrders.SingleAsync(TestContext.CurrentContext.CancellationToken);
    stationOrder.IsHiddenFromAsItComesQueue = true;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    ErrorOr<StationQueueView> queue = await _service.ReadQueueAsync(_seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(queue.Value.Orders, Has.Count.EqualTo(1));
                      Assert.That(queue.Value.AsItComes, Is.Empty);
                    });
  }

  [Test]
  public async Task ReadQueueAsync_ATogetherOrderMarkedHidden_StaysOutOfTheSecondColumnAllTheSame()
  {
    await PlaceOrderAsync(_seeded.KitchenStationId, "Tisch 12", 1, DeliveryMode.Together);
    var stationOrder = await _fixture.DbContext.StationOrders.SingleAsync(TestContext.CurrentContext.CancellationToken);
    stationOrder.IsHiddenFromAsItComesQueue = true;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    ErrorOr<StationQueueView> queue = await _service.ReadQueueAsync(_seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(queue.Value.Orders, Has.Count.EqualTo(1));
                      Assert.That(queue.Value.AsItComes, Is.Empty);
                    });
  }

  [Test]
  public async Task ReadQueueAsync_ATableRenamedAfterTheOrder_CarriesTheTablesCurrentName()
  {
    await PlaceOrderAsync(_seeded.KitchenStationId, "Tisch 12", 1, DeliveryMode.Together);
    var table = await _fixture.DbContext.Tables.SingleAsync(TestContext.CurrentContext.CancellationToken);
    table.Name = "Stammtisch";
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    ErrorOr<StationQueueView> queue = await _service.ReadQueueAsync(_seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.That(queue.Value.Orders.Single().TableName, Is.EqualTo("Stammtisch"));
  }

  [Test]
  public async Task ReadQueueAsync_AStationOrderThatIsCompletelyDone_LeavesItOut()
  {
    IReadOnlyList<Guid> items = await PlaceOrderAsync(_seeded.KitchenStationId, "Tisch 12", 1, DeliveryMode.Together);
    await StampAsync(items);

    ErrorOr<StationQueueView> queue = await _service.ReadQueueAsync(_seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.That(queue.Value.Orders, Is.Empty);
  }

  [Test]
  public async Task ReadQueueAsync_AStationOrderOfAnotherStation_LeavesItOut()
  {
    await PlaceOrderAsync(_seeded.BarStationId, "Tisch 12", 1, DeliveryMode.Together);

    ErrorOr<StationQueueView> queue = await _service.ReadQueueAsync(_seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.That(queue.Value.Orders, Is.Empty);
  }

  [Test]
  public async Task ReadQueueAsync_AStationOrderOfAnotherFestival_LeavesItOut()
  {
    var otherFestivalId = await AddOtherFestivalAsync();
    await PlaceOrderAsync(_seeded.KitchenStationId, "Tisch 12", 1, DeliveryMode.Together, otherFestivalId);

    ErrorOr<StationQueueView> queue = await _service.ReadQueueAsync(_seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.That(queue.Value.Orders, Is.Empty);
  }

  [Test]
  public async Task ReadQueueAsync_AStationNobodyHasOrderedFrom_StillNamesTheStation()
  {
    ErrorOr<StationQueueView> queue = await _service.ReadQueueAsync(_seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(queue.IsSuccess, Is.True);
                      Assert.That(queue.Value.Station.Id, Is.EqualTo(_seeded.KitchenStationId));
                      Assert.That(queue.Value.Orders, Is.Empty);
                      Assert.That(queue.Value.AsItComes, Is.Empty);
                    });
  }

  [Test]
  public async Task ReadQueueAsync_AStationThatIsNotThere_IsRefusedBecauseTheStationIsUnknown()
  {
    ErrorOr<StationQueueView> queue = await _service.ReadQueueAsync(Guid.NewGuid(), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(queue.IsSuccess, Is.False);
                      Assert.That(queue.RefusalMessageKey(), Is.EqualTo("StationUnknown"));
                    });
  }

  [Test]
  public async Task ReadQueueAsync_NoFestivalIsRunning_IsRefusedWithTheReasonNoFestivalIsRunning()
  {
    await EndTheFestivalAsync();

    ErrorOr<StationQueueView> queue = await _service.ReadQueueAsync(_seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(queue.IsSuccess, Is.False);
                      Assert.That(queue.RefusalMessageKey(), Is.EqualTo("errors.station.noFestivalIsRunning"));
                    });
  }

  [Test]
  public async Task ReadQueueAsync_TheStationIsNotAtTheRunningFestival_IsRefusedBecauseItDoesNotTakePart()
  {
    await TakeTheKitchenOutOfTheFestivalAsync();

    ErrorOr<StationQueueView> queue = await _service.ReadQueueAsync(_seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(queue.IsSuccess, Is.False);
                      Assert.That(queue.RefusalMessageKey(), Is.EqualTo("errors.station.notPartOfTheFestival"));
                    });
  }

  [Test]
  public async Task ReadQueueAsync_AnUnknownStationAndNoRunningFestival_IsRefusedAsUnknownStationFirst()
  {
    await EndTheFestivalAsync();

    ErrorOr<StationQueueView> queue = await _service.ReadQueueAsync(Guid.NewGuid(), TestContext.CurrentContext.CancellationToken);

    Assert.That(queue.RefusalMessageKey(), Is.EqualTo("StationUnknown"));
  }

  [Test]
  public async Task ReadQueueAsync_AStationWithTwoOpenOrders_RunsFourStatementsAndNoMore()
  {
    await PlaceOrderAsync(_seeded.KitchenStationId, "Tisch 12", 1, DeliveryMode.AsItComes);
    await PlaceOrderAsync(_seeded.KitchenStationId, "Tisch 3", 2, DeliveryMode.Together);
    QueryCounter counter = new();
    await using GastronomyAppDbContext readContext = new(new DbContextOptionsBuilder<GastronomyAppDbContext>().UseSqlite(_fixture.Connection).AddInterceptors(counter).Options);

    await CreateService(readContext).ReadQueueAsync(_seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.That(counter.QueryCount, Is.EqualTo(4));
  }

  [Test]
  public async Task ReadFulfilledAsync_AStationOrderWithOneItemHandedOut_ListsItWithEveryLine()
  {
    IReadOnlyList<Guid> items = await PlaceOrderAsync(_seeded.KitchenStationId, "Tisch 12", 1, DeliveryMode.Together);
    await StampAsync([items[0]]);

    ErrorOr<StationFulfilledView> fulfilled = await _service.ReadFulfilledAsync(_seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    var order = fulfilled.Value.StationOrders.Single();

    Assert.Multiple(() =>
                    {
                      Assert.That(order.Items, Has.Count.EqualTo(2));
                      Assert.That(order.Items.Count(item => item.FulfilledAtUtc != null), Is.EqualTo(1));
                      Assert.That(order.StaffMemberName, Is.EqualTo("Anna"));
                      Assert.That(order.Items.Single(item => item.OrderItemId == items[0]).FulfilledAtUtc, Is.EqualTo(_orderedAtUtc.AddMinutes(5)));
                    });
  }

  [Test]
  public async Task ReadFulfilledAsync_SeveralStationOrders_ListsThemInTheOrderTheStationNumbersThem()
  {
    IReadOnlyList<Guid> later = await PlaceOrderAsync(_seeded.KitchenStationId, "Tisch 12", 2, DeliveryMode.Together);
    IReadOnlyList<Guid> earlier = await PlaceOrderAsync(_seeded.KitchenStationId, "Tisch 3", 1, DeliveryMode.Together);
    await StampAsync([
                       later[0],
                       earlier[0]
                     ]);

    ErrorOr<StationFulfilledView> fulfilled = await _service.ReadFulfilledAsync(_seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.That(fulfilled.Value.StationOrders.Select(stationOrder => stationOrder.TableName),
                Is.EqualTo(new[]
                           {
                             "Tisch 3",
                             "Tisch 12"
                           }));
  }

  [Test]
  public async Task ReadFulfilledAsync_AStationOrderNobodyHasTouchedAndOneOfAnotherStation_LeavesBothOut()
  {
    await PlaceOrderAsync(_seeded.KitchenStationId, "Tisch 12", 1, DeliveryMode.Together);
    IReadOnlyList<Guid> barItems = await PlaceOrderAsync(_seeded.BarStationId, "Tisch 3", 2, DeliveryMode.Together);
    await StampAsync(barItems);

    ErrorOr<StationFulfilledView> fulfilled = await _service.ReadFulfilledAsync(_seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.That(fulfilled.Value.StationOrders, Is.Empty);
  }

  [Test]
  public async Task ReadFulfilledAsync_TheStationIsNotAtTheRunningFestival_IsRefusedBecauseItDoesNotTakePart()
  {
    await TakeTheKitchenOutOfTheFestivalAsync();

    ErrorOr<StationFulfilledView> fulfilled = await _service.ReadFulfilledAsync(_seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(fulfilled.IsSuccess, Is.False);
                      Assert.That(fulfilled.RefusalMessageKey(), Is.EqualTo("errors.station.notPartOfTheFestival"));
                    });
  }

  [Test]
  public void FulfillAsync_NullSelection_ThrowsArgumentNullException()
  {
    Assert.That(async () => await _service.FulfillAsync(null!, _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken), Throws.ArgumentNullException);
  }

  [Test]
  public async Task FulfillAsync_OpenItemsAtThisStation_StampsTheClockOnEachOfThemAndAnswersWithTheFreshQueue()
  {
    IReadOnlyList<Guid> items = await PlaceOrderAsync(_seeded.KitchenStationId, "Tisch 12", 1, DeliveryMode.Together);

    ErrorOr<StationQueueView> queue = await _service.FulfillAsync([items[0]], _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    var stored = await StoredFulfilmentsAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(queue.IsSuccess, Is.True);
                      Assert.That(queue.Value.Station.Id, Is.EqualTo(_seeded.KitchenStationId));
                      Assert.That(queue.Value.Orders.Single().FulfilledItemCount, Is.EqualTo(1));
                      Assert.That(stored[items[0]], Is.EqualTo(_clock.GetUtcNow().UtcDateTime));
                      Assert.That(stored[items[1]], Is.Null);
                    });
  }

  [Test]
  public async Task FulfillAsync_EveryItemOfAStationOrder_LeavesItOutOfBothColumns()
  {
    IReadOnlyList<Guid> items = await PlaceOrderAsync(_seeded.KitchenStationId, "Tisch 12", 1, DeliveryMode.AsItComes);

    ErrorOr<StationQueueView> queue = await _service.FulfillAsync(items, _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(queue.Value.Orders, Is.Empty);
                      Assert.That(queue.Value.AsItComes, Is.Empty);
                    });
  }

  [Test]
  public async Task FulfillAsync_ItemsOfTwoStationOrders_StampsThemAll()
  {
    IReadOnlyList<Guid> first = await PlaceOrderAsync(_seeded.KitchenStationId, "Tisch 12", 1, DeliveryMode.Together);
    IReadOnlyList<Guid> second = await PlaceOrderAsync(_seeded.KitchenStationId, "Tisch 3", 2, DeliveryMode.Together);

    await _service.FulfillAsync([
                                  first[0],
                                  first[1],
                                  second[0]
                                ],
                                _seeded.KitchenStationId,
                                TestContext.CurrentContext.CancellationToken);

    var stored = await StoredFulfilmentsAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(stored[first[0]], Is.Not.Null);
                      Assert.That(stored[first[1]], Is.Not.Null);
                      Assert.That(stored[second[0]], Is.Not.Null);
                      Assert.That(stored[second[1]], Is.Null);
                    });
  }

  [Test]
  public async Task FulfillAsync_AnItemAnotherTapHadAlreadyHandedOut_KeepsItsExistingTimestampAndStillSucceeds()
  {
    IReadOnlyList<Guid> items = await PlaceOrderAsync(_seeded.KitchenStationId, "Tisch 12", 1, DeliveryMode.Together);
    await StampAsync([items[0]]);

    ErrorOr<StationQueueView> queue = await _service.FulfillAsync([
                                                                    items[0],
                                                                    items[1]
                                                                  ],
                                                                  _seeded.KitchenStationId,
                                                                  TestContext.CurrentContext.CancellationToken);

    var stored = await StoredFulfilmentsAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(queue.IsSuccess, Is.True);
                      Assert.That(stored[items[0]], Is.EqualTo(_orderedAtUtc.AddMinutes(5)));
                      Assert.That(stored[items[1]], Is.EqualTo(_clock.GetUtcNow().UtcDateTime));
                    });
  }

  [Test]
  public async Task FulfillAsync_TheSameItemNamedTwice_HandsItOutOnce()
  {
    IReadOnlyList<Guid> items = await PlaceOrderAsync(_seeded.KitchenStationId, "Tisch 12", 1, DeliveryMode.Together);

    ErrorOr<StationQueueView> queue = await _service.FulfillAsync([
                                                                    items[0],
                                                                    items[0]
                                                                  ],
                                                                  _seeded.KitchenStationId,
                                                                  TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(queue.IsSuccess, Is.True);
                      Assert.That(queue.Value.Orders.Single().FulfilledItemCount, Is.EqualTo(1));
                    });
  }

  [Test]
  public async Task FulfillAsync_TheSameSelectionOnASecondTap_KeepsTheFirstTimestamp()
  {
    IReadOnlyList<Guid> items = await PlaceOrderAsync(_seeded.KitchenStationId, "Tisch 12", 1, DeliveryMode.Together);
    await _service.FulfillAsync([items[0]], _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);
    var firstStamp = (await StoredFulfilmentsAsync())[items[0]];
    _clock.Advance(TimeSpan.FromMinutes(3));

    await using var secondTapContext = _fixture.CreateContext();

    await CreateService(secondTapContext).FulfillAsync([items[0]], _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.That((await StoredFulfilmentsAsync())[items[0]], Is.EqualTo(firstStamp));
  }

  [Test]
  public async Task FulfillAsync_AnIdThatIsNotAmongTheItemsOfThisStation_IsRefusedNamingItAndChangesNothing()
  {
    IReadOnlyList<Guid> kitchenItems = await PlaceOrderAsync(_seeded.KitchenStationId, "Tisch 12", 1, DeliveryMode.Together);
    IReadOnlyList<Guid> barItems = await PlaceOrderAsync(_seeded.BarStationId, "Tisch 12", 2, DeliveryMode.Together);

    ErrorOr<StationQueueView> queue = await _service.FulfillAsync([
                                                                    kitchenItems[0],
                                                                    barItems[0]
                                                                  ],
                                                                  _seeded.KitchenStationId,
                                                                  TestContext.CurrentContext.CancellationToken);

    var stored = await StoredFulfilmentsAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(queue.IsSuccess, Is.False);
                      Assert.That(queue.RefusalMessageKey(), Is.EqualTo("errors.station.itemNotAtThisStation"));
                      Assert.That(queue.RefusalDescription(), Does.Contain(barItems[0].ToString()));
                      Assert.That(stored.Values, Is.All.Null);
                    });
  }

  [Test]
  public async Task FulfillAsync_SeveralIdsThatAreNotAmongTheItemsOfThisStation_ReportsEachOneInTheOrderSent()
  {
    IReadOnlyList<Guid> kitchenItems = await PlaceOrderAsync(_seeded.KitchenStationId, "Tisch 12", 1, DeliveryMode.Together);
    var firstStranger = Guid.NewGuid();
    var secondStranger = Guid.NewGuid();

    ErrorOr<StationQueueView> queue = await _service.FulfillAsync([
                                                                    firstStranger,
                                                                    kitchenItems[0],
                                                                    secondStranger
                                                                  ],
                                                                  _seeded.KitchenStationId,
                                                                  TestContext.CurrentContext.CancellationToken);

    IReadOnlyList<Error> lines = queue.RefusedLines();

    Assert.Multiple(() =>
                    {
                      Assert.That(lines, Has.Count.EqualTo(2));
                      Assert.That(lines[0].Description, Does.Contain(firstStranger.ToString()));
                      Assert.That(lines[1].Description, Does.Contain(secondStranger.ToString()));
                    });
  }

  [Test]
  public async Task FulfillAsync_NoFestivalIsRunning_IsRefusedAndWritesNothing()
  {
    IReadOnlyList<Guid> items = await PlaceOrderAsync(_seeded.KitchenStationId, "Tisch 12", 1, DeliveryMode.Together);
    await EndTheFestivalAsync();

    ErrorOr<StationQueueView> queue = await _service.FulfillAsync([items[0]], _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    var stored = await StoredFulfilmentsAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(queue.RefusalMessageKey(), Is.EqualTo("errors.station.noFestivalIsRunning"));
                      Assert.That(stored.Values, Is.All.Null);
                    });
  }

  [Test]
  public void UnfulfillAsync_NullSelection_ThrowsArgumentNullException()
  {
    Assert.That(async () => await _service.UnfulfillAsync(null!, _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken), Throws.ArgumentNullException);
  }

  [Test]
  public async Task UnfulfillAsync_ItemsTheStationHandedOut_ClearsTheStampAndAnswersWithTheFreshQueue()
  {
    IReadOnlyList<Guid> items = await PlaceOrderAsync(_seeded.KitchenStationId, "Tisch 12", 1, DeliveryMode.Together);
    await StampAsync(items);

    ErrorOr<StationQueueView> queue = await _service.UnfulfillAsync(items, _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    var stored = await StoredFulfilmentsAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(queue.IsSuccess, Is.True);
                      Assert.That(queue.Value.Orders.Single().FulfilledItemCount, Is.Zero);
                      Assert.That(stored.Values, Is.All.Null);
                    });
  }

  [Test]
  public async Task UnfulfillAsync_AnItemThatIsStillOpen_IsRefusedNamingItAndChangesNothing()
  {
    IReadOnlyList<Guid> items = await PlaceOrderAsync(_seeded.KitchenStationId, "Tisch 12", 1, DeliveryMode.Together);
    await StampAsync([items[0]]);

    ErrorOr<StationQueueView> queue = await _service.UnfulfillAsync([
                                                                      items[0],
                                                                      items[1]
                                                                    ],
                                                                    _seeded.KitchenStationId,
                                                                    TestContext.CurrentContext.CancellationToken);

    var stored = await StoredFulfilmentsAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(queue.IsSuccess, Is.False);
                      Assert.That(queue.RefusalMessageKey(), Is.EqualTo("errors.station.changeNotSaved"));
                      Assert.That(queue.RefusalDescription(), Does.Contain(items[1].ToString()));
                      Assert.That(stored[items[0]], Is.EqualTo(_orderedAtUtc.AddMinutes(5)));
                    });
  }

  [Test]
  public async Task UnfulfillAsync_AnIdThatIsNotAmongTheItemsOfThisStation_IsRefusedNamingIt()
  {
    IReadOnlyList<Guid> items = await PlaceOrderAsync(_seeded.KitchenStationId, "Tisch 12", 1, DeliveryMode.Together);
    IReadOnlyList<Guid> barItems = await PlaceOrderAsync(_seeded.BarStationId, "Tisch 12", 2, DeliveryMode.Together);
    await StampAsync([
                       items[0],
                       barItems[0]
                     ]);

    ErrorOr<StationQueueView> queue = await _service.UnfulfillAsync([
                                                                      items[0],
                                                                      barItems[0]
                                                                    ],
                                                                    _seeded.KitchenStationId,
                                                                    TestContext.CurrentContext.CancellationToken);

    var stored = await StoredFulfilmentsAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(queue.RefusalMessageKey(), Is.EqualTo("errors.station.itemNotAtThisStation"));
                      Assert.That(queue.RefusalDescription(), Does.Contain(barItems[0].ToString()));
                      Assert.That(stored[items[0]], Is.Not.Null);
                      Assert.That(stored[barItems[0]], Is.Not.Null);
                    });
  }

  [Test]
  public async Task UnfulfillAsync_AnUnknownIdBeforeAnOpenItem_ReportsBothInTheOrderSent()
  {
    IReadOnlyList<Guid> items = await PlaceOrderAsync(_seeded.KitchenStationId, "Tisch 12", 1, DeliveryMode.Together);
    var stranger = Guid.NewGuid();

    ErrorOr<StationQueueView> queue = await _service.UnfulfillAsync([
                                                                      stranger,
                                                                      items[0]
                                                                    ],
                                                                    _seeded.KitchenStationId,
                                                                    TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(queue.RefusedLines().Select(line => line.Code),
                                  Is.EqualTo(new[]
                                             {
                                               "errors.station.itemNotAtThisStation",
                                               "errors.station.changeNotSaved"
                                             }));
                      Assert.That(queue.RefusedLines()[0].Description, Does.Contain(stranger.ToString()));
                      Assert.That(queue.RefusedLines()[1].Description, Does.Contain(items[0].ToString()));
                    });
  }

  [Test]
  public async Task UnfulfillAsync_TheStationIsNotAtTheRunningFestival_IsRefusedBecauseItDoesNotTakePart()
  {
    IReadOnlyList<Guid> items = await PlaceOrderAsync(_seeded.KitchenStationId, "Tisch 12", 1, DeliveryMode.Together);
    await StampAsync(items);
    await TakeTheKitchenOutOfTheFestivalAsync();

    ErrorOr<StationQueueView> queue = await _service.UnfulfillAsync(items, _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    var stored = await StoredFulfilmentsAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(queue.RefusalMessageKey(), Is.EqualTo("errors.station.notPartOfTheFestival"));
                      Assert.That(stored.Values, Is.All.Not.Null);
                    });
  }

  [Test]
  public async Task HideFromAsItComesQueueAsync_AnAsItComesOrderAtThisStation_HidesItFromTheSecondColumnOnlyAndAnswersWithTheQueue()
  {
    await PlaceOrderAsync(_seeded.KitchenStationId, "Tisch 12", 1, DeliveryMode.AsItComes);
    var stationOrderId = (await _fixture.DbContext.StationOrders.SingleAsync(TestContext.CurrentContext.CancellationToken)).Id;

    ErrorOr<StationQueueView> queue = await _service.HideFromAsItComesQueueAsync(stationOrderId, _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    await using var readContext = _fixture.CreateContext();
    var stored = await readContext.StationOrders.SingleAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(queue.IsSuccess, Is.True);
                      Assert.That(queue.Value.Orders, Has.Count.EqualTo(1));
                      Assert.That(queue.Value.Orders[0].IsHiddenFromAsItComesQueue, Is.True);
                      Assert.That(queue.Value.AsItComes, Is.Empty);
                      Assert.That(stored.IsHiddenFromAsItComesQueue, Is.True);
                    });
  }

  [Test]
  public async Task HideFromAsItComesQueueAsync_AnAlreadyHiddenOrder_IsAccepted()
  {
    await PlaceOrderAsync(_seeded.KitchenStationId, "Tisch 12", 1, DeliveryMode.AsItComes);
    var stationOrder = await _fixture.DbContext.StationOrders.SingleAsync(TestContext.CurrentContext.CancellationToken);
    stationOrder.IsHiddenFromAsItComesQueue = true;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    ErrorOr<StationQueueView> queue = await _service.HideFromAsItComesQueueAsync(stationOrder.Id, _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.That(queue.IsSuccess, Is.True);
  }

  [Test]
  public async Task HideFromAsItComesQueueAsync_ATogetherOrder_IsRefusedAndLeavesTheFlagOff()
  {
    await PlaceOrderAsync(_seeded.KitchenStationId, "Tisch 12", 1, DeliveryMode.Together);
    var stationOrderId = (await _fixture.DbContext.StationOrders.SingleAsync(TestContext.CurrentContext.CancellationToken)).Id;

    ErrorOr<StationQueueView> queue = await _service.HideFromAsItComesQueueAsync(stationOrderId, _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    await using var readContext = _fixture.CreateContext();
    var stored = await readContext.StationOrders.SingleAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(queue.IsSuccess, Is.False);
                      Assert.That(queue.RefusalMessageKey(), Is.EqualTo("errors.station.changeNotSaved"));
                      Assert.That(stored.IsHiddenFromAsItComesQueue, Is.False);
                    });
  }

  [Test]
  public async Task HideFromAsItComesQueueAsync_AnOrderOfAnotherStation_IsRefusedBecauseItIsNotAtThisStation()
  {
    await PlaceOrderAsync(_seeded.BarStationId, "Tisch 12", 1, DeliveryMode.AsItComes);
    var barStationOrderId = (await _fixture.DbContext.StationOrders.SingleAsync(TestContext.CurrentContext.CancellationToken)).Id;

    ErrorOr<StationQueueView> queue = await _service.HideFromAsItComesQueueAsync(barStationOrderId, _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    await using var readContext = _fixture.CreateContext();
    var stored = await readContext.StationOrders.SingleAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(queue.RefusalMessageKey(), Is.EqualTo("errors.station.orderNotAtThisStation"));
                      Assert.That(stored.IsHiddenFromAsItComesQueue, Is.False);
                    });
  }

  [Test]
  public async Task HideFromAsItComesQueueAsync_TheStationIsNotAtTheRunningFestival_IsRefusedBeforeItReadsTheOrder()
  {
    await PlaceOrderAsync(_seeded.KitchenStationId, "Tisch 12", 1, DeliveryMode.AsItComes);
    var stationOrderId = (await _fixture.DbContext.StationOrders.SingleAsync(TestContext.CurrentContext.CancellationToken)).Id;
    await TakeTheKitchenOutOfTheFestivalAsync();

    ErrorOr<StationQueueView> queue = await _service.HideFromAsItComesQueueAsync(stationOrderId, _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    await using var readContext = _fixture.CreateContext();
    var stored = await readContext.StationOrders.SingleAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(queue.RefusalMessageKey(), Is.EqualTo("errors.station.notPartOfTheFestival"));
                      Assert.That(stored.IsHiddenFromAsItComesQueue, Is.False);
                    });
  }

  private StationQueueService CreateService(GastronomyAppDbContext database)
  {
    RunningFestivalLookup runningFestival = new(new FestivalRepository(database, new()), new(), _clock);

    return new(database, new CoreMappingFactory().Create(), runningFestival, _clock);
  }

  private async Task EndTheFestivalAsync()
  {
    var festival = await _fixture.DbContext.Festivals.SingleAsync(candidate => candidate.Id == _seeded.FestivalId, TestContext.CurrentContext.CancellationToken);
    festival.StartsAtUtc = DateTime.UtcNow.AddDays(-2);
    festival.EndsAtUtc = DateTime.UtcNow.AddHours(-1);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }

  private async Task TakeTheKitchenOutOfTheFestivalAsync()
  {
    var link = await _fixture.DbContext.FestivalStations.SingleAsync(candidate => candidate.StationId == _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.FestivalStations.Remove(link);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }

  private async Task<Guid> AddOtherFestivalAsync()
  {
    var festivalId = Guid.NewGuid();
    _fixture.DbContext.Festivals.Add(new()
                                     {
                                       Id = festivalId,
                                       Name = "Herbstfest",
                                       StartsAtUtc = DateTime.UtcNow.AddDays(-60),
                                       EndsAtUtc = DateTime.UtcNow.AddDays(-59),
                                       NextOrderNumber = 1,
                                       IsHidden = false
                                     });
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return festivalId;
  }

  private async Task StampAsync(IReadOnlyCollection<Guid> orderItemIds)
  {
    List<Guid> ids = orderItemIds.ToList();
    List<OrderItem> items = await _fixture.DbContext.OrderItems.Where(item => ids.Contains(item.Id)).ToListAsync(TestContext.CurrentContext.CancellationToken);

    foreach (var item in items)
      item.FulfilledAtUtc = _orderedAtUtc.AddMinutes(5);

    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }

  private async Task<Dictionary<Guid, DateTime?>> StoredFulfilmentsAsync()
  {
    await using var readContext = _fixture.CreateContext();

    return await readContext.OrderItems.AsNoTracking().ToDictionaryAsync(item => item.Id, item => item.FulfilledAtUtc, TestContext.CurrentContext.CancellationToken);
  }

  private async Task<IReadOnlyList<Guid>> PlaceOrderAsync(Guid stationId, string tableName, int orderNumber, DeliveryMode deliveryMode, Guid? festivalId = null)
  {
    var stationOrderId = await PlaceBareStationOrderAsync(stationId, tableName, orderNumber, deliveryMode, festivalId);

    List<OrderItem> items =
    [
      BuildItem(stationOrderId, _seeded.SausageItemId, "Bratwurst", 350, "Ohne Ketchup", Guid.NewGuid()),
      BuildItem(stationOrderId, _seeded.LemonadeItemId, "Limonade", 250, null, Guid.NewGuid())
    ];
    _fixture.DbContext.OrderItems.AddRange(items);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return items.Select(item => item.Id).ToList();
  }

  private async Task<Guid> PlaceBareStationOrderAsync(Guid stationId, string tableName, int orderNumber, DeliveryMode deliveryMode, Guid? festivalId = null)
  {
    var festival = festivalId ?? _seeded.FestivalId;
    var tableId = await new DomainSeeder().SeedTableAsync(_fixture.DbContext, festival, tableName, TestContext.CurrentContext.CancellationToken);
    var orderId = Guid.NewGuid();
    var stationOrderId = Guid.NewGuid();

    Order order = new()
                  {
                    Id = orderId,
                    ClientOrderId = Guid.NewGuid(),
                    FestivalId = festival,
                    GlobalOrderNumber = orderNumber,
                    StaffMemberId = _seeded.StaffMemberId,
                    TableId = tableId,
                    CreatedAtUtc = _orderedAtUtc
                  };

    order.StationOrders.Add(new()
                            {
                              Id = stationOrderId,
                              OrderId = orderId,
                              FestivalId = festival,
                              StationId = stationId,
                              StationOrderNumber = orderNumber,
                              DeliveryMode = deliveryMode
                            });

    _fixture.DbContext.Orders.Add(order);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return stationOrderId;
  }

  private async Task AddItemsAsync(Guid stationOrderId, params OrderItem[] items)
  {
    foreach (var item in items)
    {
      item.StationOrderId = stationOrderId;
      _fixture.DbContext.OrderItems.Add(item);
    }

    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }

  private OrderItem Line(string itemName, string? note)
  {
    return BuildItem(Guid.Empty, _seeded.SausageItemId, itemName, 100, note, Guid.NewGuid());
  }

  private OrderItem BuildItem(Guid stationOrderId, Guid catalogItemId, string itemName, int unitPriceCents, string? note, Guid id)
  {
    return new()
           {
             Id = id,
             StationOrderId = stationOrderId,
             CatalogItemId = catalogItemId,
             ItemName = itemName,
             UnitPriceCents = unitPriceCents,
             Note = note
           };
  }
}
