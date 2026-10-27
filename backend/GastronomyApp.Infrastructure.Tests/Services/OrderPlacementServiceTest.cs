using System.Text.Json;
using ErrorOr;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Contracts.Orders;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace GastronomyApp.Infrastructure.Tests.Services;

[TestFixture]
public sealed class OrderPlacementServiceTest
{
  [SetUp]
  public async Task SetUp()
  {
    _fixture = new();
    _seeded = await new DomainSeeder().SeedAsync(_fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    _tableId = await new DomainSeeder().SeedTableAsync(_fixture.DbContext, _seeded.FestivalId, "Tisch 12", TestContext.CurrentContext.CancellationToken);
    _clock = new(DateTimeOffset.UtcNow);
    RunningFestivalLookup runningFestival = new(_fixture.DbContext, new(), _clock);
    _service = new(_fixture.DbContext, new CoreMappingFactory().Create(), runningFestival, new(_fixture.DbContext), new(), new(_fixture.DbContext, new(), NullLogger<StockSoldOutMarker>.Instance), new(), _clock);
  }

  [TearDown]
  public void TearDown()
  {
    _fixture.Dispose();
  }

  private FakeTimeProvider _clock = null!;
  private SqliteInMemoryFixture _fixture = null!;
  private SeededDomain _seeded = null!;
  private OrderPlacementService _service = null!;
  private Guid _tableId;

  [Test]
  public void PlaceAsync_NullRequest_ThrowsArgumentNullException()
  {
    Assert.That(async () => await _service.PlaceAsync(null!, _seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken), Throws.ArgumentNullException);
  }

  [Test]
  public async Task PlaceAsync_NewClientOrderId_StoresOrderStationOrdersAndLinesAndAnswersWithThem()
  {
    ErrorOr<PlacedOrderView> result = await PlaceAsync(RequestWith(ItemFor(_seeded.SausageItemId), ItemFor(_seeded.LemonadeItemId)));

    var stored = await StoredOrderAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(result.IsSuccess, Is.True);
                      Assert.That(result.Value.OrderId, Is.EqualTo(stored.Id));
                      Assert.That(result.Value.GlobalOrderNumber, Is.EqualTo(1));
                      Assert.That(result.Value.Status, Is.EqualTo(OrderStatus.Open));
                      Assert.That(result.Value.TotalCents, Is.EqualTo(700));
                      Assert.That(result.Value.StationOrders, Has.Count.EqualTo(2));
                      Assert.That(result.Value.StationOrders.SelectMany(stationOrder => stationOrder.ItemIds), Is.EquivalentTo(stored.StationOrders.SelectMany(stationOrder => stationOrder.Items).Select(item => item.Id)));
                      Assert.That(stored.StaffMemberId, Is.EqualTo(_seeded.StaffMemberId));
                      Assert.That(stored.TableId, Is.EqualTo(_tableId));
                      Assert.That(stored.CreatedAtUtc, Is.EqualTo(_clock.GetUtcNow().UtcDateTime));
                    });
  }

  [Test]
  public async Task PlaceAsync_TheSameItemTwice_StoresOneRowPerItem()
  {
    await PlaceAsync(RequestWith(ItemFor(_seeded.SausageItemId), ItemFor(_seeded.SausageItemId)));

    var stored = await StoredOrderAsync();

    Assert.That(stored.StationOrders.SelectMany(stationOrder => stationOrder.Items).ToList(), Has.Count.EqualTo(2));
  }

  [Test]
  public async Task PlaceAsync_TwoLinesOfOneArticleWithDifferentNotes_KeepsBothNotesOnTheirOwnLine()
  {
    await PlaceAsync(RequestWith(ItemFor(_seeded.SausageItemId, note: "Ohne Ketchup"), ItemFor(_seeded.SausageItemId)));

    var stored = await StoredOrderAsync();

    Assert.That(stored.StationOrders.SelectMany(stationOrder => stationOrder.Items).Select(item => item.Note),
                Is.EquivalentTo(new string?[]
                                {
                                  "Ohne Ketchup",
                                  null
                                }));
  }

  [Test]
  public async Task PlaceAsync_ValidRequest_KeepsTheNameFromTheCatalogAndThePriceThePhoneShowed()
  {
    await PlaceAsync(RequestWith(ItemFor(_seeded.SausageItemId, unitPriceCents: 399), ItemFor(_seeded.LemonadeItemId, unitPriceCents: 1)));

    var items = (await StoredOrderAsync()).StationOrders.SelectMany(stationOrder => stationOrder.Items).ToList();

    Assert.Multiple(() =>
                    {
                      Assert.That(items.Single(item => item.CatalogItemId == _seeded.SausageItemId).ItemName, Is.EqualTo("Bratwurst"));
                      Assert.That(items.Single(item => item.CatalogItemId == _seeded.SausageItemId).UnitPriceCents, Is.EqualTo(399));
                      Assert.That(items.Single(item => item.CatalogItemId == _seeded.LemonadeItemId).UnitPriceCents, Is.EqualTo(1));
                    });
  }

  [Test]
  public async Task PlaceAsync_ValidRequest_StoresEveryItemWithItsFirstOpenStatusSetByTheWaiterAtTheOrderTime()
  {
    await PlaceAsync(RequestWith(ItemFor(_seeded.SausageItemId), ItemFor(_seeded.LemonadeItemId)));

    var stored = await StoredOrderAsync();
    List<OrderItemStatus> statuses = await _fixture.DbContext.OrderItemStatuses.AsNoTracking().ToListAsync(TestContext.CurrentContext.CancellationToken);
    List<OrderItem> items = stored.StationOrders.SelectMany(stationOrder => stationOrder.Items).ToList();

    Assert.Multiple(() =>
                    {
                      Assert.That(statuses, Has.Count.EqualTo(2));
                      Assert.That(items.Select(item => item.CurrentStatusId), Is.All.Not.Null);
                      Assert.That(items.Select(item => item.CurrentStatusId), Is.EquivalentTo(statuses.Select(status => (Guid?)status.Id)));
                      Assert.That(statuses.Select(status => status.OrderItemId), Is.EquivalentTo(items.Select(item => item.Id)));
                      Assert.That(statuses.Select(status => status.Status), Is.All.EqualTo(OrderItemState.Open));
                      Assert.That(statuses.Select(status => status.PreviousStatusId), Is.All.Null);
                      Assert.That(statuses.Select(status => status.CreatedByStaffMemberId), Is.All.EqualTo(_seeded.StaffMemberId));
                      Assert.That(statuses.Select(status => status.CreatedByStationId), Is.All.Null);
                      Assert.That(statuses.Select(status => status.CreatedAtUtc), Is.All.EqualTo(stored.CreatedAtUtc));
                    });
  }

  [Test]
  public async Task PlaceAsync_ValidRequest_LeavesEveryItemOpenAndUnsettledWithItsOwnIdentifier()
  {
    await PlaceAsync(RequestWith(ItemFor(_seeded.SausageItemId), ItemFor(_seeded.LemonadeItemId)));

    var stored = await StoredOrderAsync();
    List<OrderItem> items = stored.StationOrders.SelectMany(stationOrder => stationOrder.Items).ToList();
    List<Guid> everyId =
    [
      stored.Id,
      .. items.Select(item => item.Id),
      .. stored.StationOrders.Select(stationOrder => stationOrder.Id)
    ];

    Assert.Multiple(() =>
                    {
                      Assert.That(items.Select(item => item.CurrentStatus!.Status), Is.All.EqualTo(OrderItemState.Open));
                      Assert.That(items.Select(item => item.SettledAtUtc), Is.All.Null);
                      Assert.That(everyId, Has.None.EqualTo(Guid.Empty));
                      Assert.That(everyId.Distinct().Count(), Is.EqualTo(everyId.Count));
                    });
  }

  [Test]
  public async Task PlaceAsync_TwoLinesAtOneStation_MakeOneStationOrderWithOneSequenceNumber()
  {
    await AssignAsync(_seeded.LemonadeItemId, _seeded.KitchenStationId, true);

    await PlaceAsync(RequestWith(ItemFor(_seeded.SausageItemId), ItemFor(_seeded.LemonadeItemId)));

    var stored = await StoredOrderAsync();
    var kitchenNext = await NextStationOrderNumberAsync(_seeded.KitchenStationId);

    Assert.Multiple(() =>
                    {
                      Assert.That(stored.StationOrders, Has.Count.EqualTo(1));
                      Assert.That(stored.StationOrders[0].Items, Has.Count.EqualTo(2));
                      Assert.That(stored.StationOrders[0].StationOrderNumber, Is.EqualTo(1));
                      Assert.That(kitchenNext, Is.EqualTo(2));
                    });
  }

  [Test]
  public async Task PlaceAsync_TwoOrders_NumberGloballyAndPerStationWithoutGapsOrRepeats()
  {
    await PlaceAsync(RequestWith(ItemFor(_seeded.SausageItemId), ItemFor(_seeded.LemonadeItemId)));
    ErrorOr<PlacedOrderView> second = await PlaceAsync(RequestWith(ItemFor(_seeded.SausageItemId)));

    var secondOrder = await _fixture.DbContext.Orders.AsNoTracking().Include(order => order.StationOrders).SingleAsync(order => order.GlobalOrderNumber == 2, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(second.Value.GlobalOrderNumber, Is.EqualTo(2));
                      Assert.That(secondOrder.StationOrders.Single().StationOrderNumber, Is.EqualTo(2));
                    });
  }

  [Test]
  public async Task PlaceAsync_StationsWithDifferentCounters_GiveEachTicketItsOwnStationsNumber()
  {
    await SetNextStationOrderNumberAsync(_seeded.KitchenStationId, 42);
    await SetNextStationOrderNumberAsync(_seeded.BarStationId, 7);

    await PlaceAsync(RequestWith(ItemFor(_seeded.SausageItemId), ItemFor(_seeded.LemonadeItemId)));

    var stored = await StoredOrderAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(stored.StationOrders.Single(stationOrder => stationOrder.StationId == _seeded.KitchenStationId).StationOrderNumber, Is.EqualTo(42));
                      Assert.That(stored.StationOrders.Single(stationOrder => stationOrder.StationId == _seeded.BarStationId).StationOrderNumber, Is.EqualTo(7));
                    });
  }

  [Test]
  public async Task PlaceAsync_NoDeliveryModeNamed_SendsEveryStationOrderTogether()
  {
    await PlaceAsync(RequestWith(ItemFor(_seeded.SausageItemId), ItemFor(_seeded.LemonadeItemId)));

    Assert.That((await StoredOrderAsync()).StationOrders.Select(stationOrder => stationOrder.DeliveryMode), Is.All.EqualTo(DeliveryMode.Together));
  }

  [Test]
  public async Task PlaceAsync_DeliveryModeNamedForOneStation_AppliesItToThatStationOrderOnly()
  {
    PlaceOrderRequest request = RequestWith(ItemFor(_seeded.SausageItemId), ItemFor(_seeded.LemonadeItemId)) with
                                {
                                  DeliveryModes =
                                  [
                                    new()
                                    {
                                      StationId = _seeded.BarStationId,
                                      DeliveryMode = DeliveryMode.AsItComes
                                    }
                                  ]
                                };

    await PlaceAsync(request);

    var stored = await StoredOrderAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(stored.StationOrders.Single(stationOrder => stationOrder.StationId == _seeded.KitchenStationId).DeliveryMode, Is.EqualTo(DeliveryMode.Together));
                      Assert.That(stored.StationOrders.Single(stationOrder => stationOrder.StationId == _seeded.BarStationId).DeliveryMode, Is.EqualTo(DeliveryMode.AsItComes));
                    });
  }

  [Test]
  public async Task PlaceAsync_AnArticleOfTwoStationsWithTheChoiceNamed_RoutesItToTheChosenStation()
  {
    await AssignAsync(_seeded.SausageItemId, _seeded.BarStationId, false);

    await PlaceAsync(RequestWith(ItemFor(_seeded.SausageItemId, _seeded.BarStationId)));

    Assert.That((await StoredOrderAsync()).StationOrders.Single().StationId, Is.EqualTo(_seeded.BarStationId));
  }

  [Test]
  public async Task PlaceAsync_AnArticleAssignedOnlyAtAnotherFestival_IsNotRoutedThere()
  {
    var lastYearId = await AddFestivalAsync();
    await AssignAsync(_seeded.SausageItemId, _seeded.BarStationId, false, lastYearId);

    await PlaceAsync(RequestWith(ItemFor(_seeded.SausageItemId)));

    Assert.That((await StoredOrderAsync()).StationOrders.Single().StationId, Is.EqualTo(_seeded.KitchenStationId));
  }

  [Test]
  public async Task PlaceAsync_AHiddenTableOfTheRunningFestival_StoresTheOrderAtThatTable()
  {
    var table = await _fixture.DbContext.Tables.SingleAsync(candidate => candidate.Id == _tableId, TestContext.CurrentContext.CancellationToken);
    table.IsHidden = true;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    ErrorOr<PlacedOrderView> result = await PlaceAsync(RequestWith(ItemFor(_seeded.SausageItemId)));

    Assert.Multiple(() =>
                    {
                      Assert.That(result.IsSuccess, Is.True);
                      Assert.That(StoredTableId(), Is.EqualTo(_tableId));
                    });
  }

  [Test]
  public async Task PlaceAsync_RepeatedClientOrderId_ReturnsTheStoredOrderAndWritesAndNumbersNothingAgain()
  {
    var clientOrderId = Guid.NewGuid();
    ErrorOr<PlacedOrderView> first = await PlaceAsync(RequestWith(clientOrderId, ItemFor(_seeded.SausageItemId), ItemFor(_seeded.LemonadeItemId)));
    _fixture.DbContext.ChangeTracker.Clear();

    ErrorOr<PlacedOrderView> second = await PlaceAsync(RequestWith(clientOrderId, ItemFor(_seeded.SausageItemId), ItemFor(_seeded.LemonadeItemId)));

    using var verification = _fixture.CreateContext();
    var orderCount = await verification.Orders.CountAsync(TestContext.CurrentContext.CancellationToken);
    var itemCount = await verification.OrderItems.CountAsync(TestContext.CurrentContext.CancellationToken);
    var nextOrderNumber = await verification.Festivals.Where(festival => festival.Id == _seeded.FestivalId).Select(festival => festival.NextOrderNumber).SingleAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(second.IsSuccess, Is.True);
                      Assert.That(Serialized(second.Value), Is.EqualTo(Serialized(first.Value)));
                      Assert.That(orderCount, Is.EqualTo(1));
                      Assert.That(itemCount, Is.EqualTo(2));
                      Assert.That(nextOrderNumber, Is.EqualTo(2));
                    });
  }

  [Test]
  public async Task PlaceAsync_RepeatedClientOrderIdWithOtherContent_ReturnsTheOrderAlreadyHeld()
  {
    var clientOrderId = Guid.NewGuid();
    ErrorOr<PlacedOrderView> first = await PlaceAsync(RequestWith(clientOrderId, ItemFor(_seeded.SausageItemId)));
    var otherTableId = await new DomainSeeder().SeedTableAsync(_fixture.DbContext, _seeded.FestivalId, "Tisch 99", TestContext.CurrentContext.CancellationToken);
    PlaceOrderRequest different = RequestWith(clientOrderId, ItemFor(_seeded.LemonadeItemId)) with { TableId = otherTableId };

    ErrorOr<PlacedOrderView> second = await PlaceAsync(different);

    var orderCount = await _fixture.DbContext.Orders.CountAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(Serialized(second.Value), Is.EqualTo(Serialized(first.Value)));
                      Assert.That(orderCount, Is.EqualTo(1));
                    });
  }

  [Test]
  public async Task PlaceAsync_RepeatedClientOrderIdAfterTheFestivalEnded_StillReturnsTheStoredOrder()
  {
    var clientOrderId = Guid.NewGuid();
    ErrorOr<PlacedOrderView> first = await PlaceAsync(RequestWith(clientOrderId, ItemFor(_seeded.SausageItemId)));
    var festival = await _fixture.DbContext.Festivals.SingleAsync(TestContext.CurrentContext.CancellationToken);
    festival.EndsAtUtc = DateTime.UtcNow.AddHours(-1);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    ErrorOr<PlacedOrderView> second = await PlaceAsync(RequestWith(clientOrderId, ItemFor(_seeded.SausageItemId)));

    Assert.That(Serialized(second.Value), Is.EqualTo(Serialized(first.Value)));
  }

  [Test]
  public async Task PlaceAsync_AReplayOfAnOrderWhoseItemWasHandedOutMeanwhile_AnswersWithTheCurrentStatus()
  {
    var clientOrderId = Guid.NewGuid();
    await PlaceAsync(RequestWith(clientOrderId, ItemFor(_seeded.SausageItemId)));
    var item = await _fixture.DbContext.OrderItems.SingleAsync(TestContext.CurrentContext.CancellationToken);
    HandOut(item);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    ErrorOr<PlacedOrderView> replay = await PlaceAsync(RequestWith(clientOrderId, ItemFor(_seeded.SausageItemId)));

    Assert.That(replay.Value.Status, Is.EqualTo(OrderStatus.Fulfilled));
  }

  [Test]
  public async Task PlaceAsync_AReplayOfAnOrderWhoseFirstItemWasHandedOutMeanwhile_AnswersWithTheCurrentStatusAndTheTotal()
  {
    var clientOrderId = Guid.NewGuid();
    await PlaceAsync(RequestWith(clientOrderId, ItemFor(_seeded.SausageItemId, unitPriceCents: 350), ItemFor(_seeded.SausageItemId, unitPriceCents: 400)));
    var item = await _fixture.DbContext.OrderItems.OrderBy(candidate => candidate.UnitPriceCents).FirstAsync(TestContext.CurrentContext.CancellationToken);
    HandOut(item);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    ErrorOr<PlacedOrderView> replay = await PlaceAsync(RequestWith(clientOrderId, ItemFor(_seeded.SausageItemId)));

    Assert.Multiple(() =>
                    {
                      Assert.That(replay.Value.Status, Is.EqualTo(OrderStatus.PartiallyFulfilled));
                      Assert.That(replay.Value.TotalCents, Is.EqualTo(750));
                    });
  }

  [Test]
  public async Task PlaceAsync_ARequestThatFailsSeveralChecksAtOnce_IsAnsweredByTheFirstCheckInOrderRetryFestivalTableArticleRouting()
  {
    var clientOrderId = Guid.NewGuid();
    var strangerTableId = Guid.NewGuid();
    await PlaceAsync(RequestWith(clientOrderId, ItemFor(_seeded.SausageItemId)));
    var festival = await _fixture.DbContext.Festivals.SingleAsync(TestContext.CurrentContext.CancellationToken);
    festival.EndsAtUtc = DateTime.UtcNow.AddHours(-1);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    ErrorOr<PlacedOrderView> retry = await PlaceAsync(RequestWith(clientOrderId, ItemFor(Guid.NewGuid())) with { TableId = strangerTableId });
    ErrorOr<PlacedOrderView> noFestival = await PlaceAsync(RequestWith(ItemFor(Guid.NewGuid())) with { TableId = strangerTableId });

    festival.EndsAtUtc = DateTime.UtcNow.AddYears(1);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    ErrorOr<PlacedOrderView> strangerTable = await PlaceAsync(RequestWith(ItemFor(Guid.NewGuid())) with { TableId = strangerTableId });

    var sausage = await _fixture.DbContext.CatalogItems.SingleAsync(item => item.Id == _seeded.SausageItemId, TestContext.CurrentContext.CancellationToken);
    var kitchen = await _fixture.DbContext.Stations.SingleAsync(station => station.Id == _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);
    sausage.IsActive = false;
    kitchen.IsActive = false;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    ErrorOr<PlacedOrderView> unavailableArticle = await PlaceAsync(RequestWith(ItemFor(_seeded.SausageItemId)));

    Assert.Multiple(() =>
                    {
                      Assert.That(retry.IsSuccess, Is.True);
                      Assert.That(noFestival.RefusalDescription(), Does.StartWith("No festival is running"));
                      Assert.That(strangerTable.RefusalDescription(), Does.Contain(strangerTableId.ToString()));
                      Assert.That(unavailableArticle.RefusalMessageKey(), Is.EqualTo("errors.order.itemSoldOut"));
                    });
  }

  [Test]
  public async Task PlaceAsync_ATableTheRunningFestivalDoesNotHold_IsRefusedAsAGuardAndStoresNothing()
  {
    var strangerTableId = Guid.NewGuid();
    PlaceOrderRequest request = RequestWith(ItemFor(_seeded.SausageItemId)) with { TableId = strangerTableId };

    ErrorOr<PlacedOrderView> result = await PlaceAsync(request);

    await AssertNothingWasNumberedOrStoredAsync();
    Assert.Multiple(() =>
                    {
                      Assert.That(result.FirstError.NumericType, Is.EqualTo(RefusalType.UnprocessableEntity));
                      Assert.That(result.RefusalMessageKey(), Is.EqualTo("errors.order.cannotBeProcessed"));
                      Assert.That(result.RefusalMetadata(Refusal.MetadataKeys.ProblemCode), Is.EqualTo(Refusal.ProblemCodes.UnprocessableEntity));
                      Assert.That(result.RefusalDescription(), Does.Contain(strangerTableId.ToString()));
                    });
  }

  [Test]
  public async Task PlaceAsync_ATableOfAnotherFestival_IsRefusedAndStoresNothing()
  {
    var otherFestivalId = await AddFestivalAsync();
    var otherFestivalsTableId = await new DomainSeeder().SeedTableAsync(_fixture.DbContext, otherFestivalId, "Tisch 12", TestContext.CurrentContext.CancellationToken);
    PlaceOrderRequest request = RequestWith(ItemFor(_seeded.SausageItemId)) with { TableId = otherFestivalsTableId };

    ErrorOr<PlacedOrderView> result = await PlaceAsync(request);

    await AssertNothingWasNumberedOrStoredAsync();
    Assert.That(result.RefusalMessageKey(), Is.EqualTo("errors.order.cannotBeProcessed"));
  }

  [Test]
  public async Task PlaceAsync_NoFestivalRunning_IsRefusedAndStoresNothing()
  {
    var festival = await _fixture.DbContext.Festivals.SingleAsync(TestContext.CurrentContext.CancellationToken);
    festival.EndsAtUtc = DateTime.UtcNow.AddHours(-1);
    festival.StartsAtUtc = DateTime.UtcNow.AddDays(-2);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    ErrorOr<PlacedOrderView> result = await PlaceAsync(RequestWith(ItemFor(_seeded.SausageItemId)));

    Assert.Multiple(() =>
                    {
                      Assert.That(result.FirstError.NumericType, Is.EqualTo(RefusalType.UnprocessableEntity));
                      Assert.That(result.RefusalMessageKey(), Is.EqualTo("errors.order.cannotBeProcessed"));
                      Assert.That(_fixture.DbContext.Orders.Count(), Is.Zero);
                    });
  }

  [Test]
  public async Task PlaceAsync_SeveralUnknownCatalogItemIds_ReportsEveryUnknownLineInTheOrderSentAndStoresNothing()
  {
    var unknownId = Guid.NewGuid();
    var secondUnknownId = Guid.NewGuid();

    ErrorOr<PlacedOrderView> result = await PlaceAsync(RequestWith(ItemFor(unknownId), ItemFor(_seeded.SausageItemId), ItemFor(secondUnknownId)));

    await AssertNothingWasNumberedOrStoredAsync();
    Assert.Multiple(() =>
                    {
                      Assert.That(result.RefusedLines().Select(line => line.Metadata!["catalogItemId"]),
                                  Is.EqualTo(new[]
                                             {
                                               unknownId.ToString(),
                                               secondUnknownId.ToString()
                                             }));
                      Assert.That(result.RefusalMessageKey(), Is.EqualTo("errors.order.unknownItem"));
                    });
  }

  [Test]
  public async Task PlaceAsync_ADeactivatedArticle_IsRefusedNamingTheArticle()
  {
    var sausage = await _fixture.DbContext.CatalogItems.SingleAsync(item => item.Id == _seeded.SausageItemId, TestContext.CurrentContext.CancellationToken);
    sausage.IsActive = false;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    ErrorOr<PlacedOrderView> result = await PlaceAsync(RequestWith(ItemFor(_seeded.SausageItemId)));

    await AssertNothingWasNumberedOrStoredAsync();
    Assert.Multiple(() =>
                    {
                      Assert.That(result.RefusalMessageKey(), Is.EqualTo("errors.order.itemSoldOut"));
                      Assert.That(result.RefusalMetadata("catalogItemId"), Is.EqualTo(_seeded.SausageItemId.ToString()));
                      Assert.That(result.RefusalMetadata("name"), Is.EqualTo("Bratwurst"));
                    });
  }

  [Test]
  public async Task PlaceAsync_AnArticleSoldOutAtTheFestival_IsRefusedNamingTheArticle()
  {
    var menuRow = await _fixture.DbContext.FestivalCatalogItems.SingleAsync(row => row.CatalogItemId == _seeded.SausageItemId, TestContext.CurrentContext.CancellationToken);
    menuRow.IsAvailable = false;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    ErrorOr<PlacedOrderView> result = await PlaceAsync(RequestWith(ItemFor(_seeded.SausageItemId)));

    await AssertNothingWasNumberedOrStoredAsync();
    Assert.Multiple(() =>
                    {
                      Assert.That(result.RefusalMessageKey(), Is.EqualTo("errors.order.itemSoldOut"));
                      Assert.That(result.RefusalMetadata("name"), Is.EqualTo("Bratwurst"));
                    });
  }

  [Test]
  public async Task PlaceAsync_AnArticleOfSeveralStationsWithNoneChosen_IsRefusedAsAGuard()
  {
    await AssignAsync(_seeded.SausageItemId, _seeded.BarStationId, false);

    ErrorOr<PlacedOrderView> result = await PlaceAsync(RequestWith(ItemFor(_seeded.SausageItemId)));

    await AssertNothingWasNumberedOrStoredAsync();
    Assert.That(result.RefusalMessageKey(), Is.EqualTo("errors.order.cannotBeProcessed"));
  }

  [Test]
  public async Task PlaceAsync_AStationThatDoesNotPrepareTheArticle_IsRefusedAsSoldOut()
  {
    ErrorOr<PlacedOrderView> result = await PlaceAsync(RequestWith(ItemFor(_seeded.SausageItemId, _seeded.BarStationId)));

    await AssertNothingWasNumberedOrStoredAsync();
    Assert.That(result.RefusalMessageKey(), Is.EqualTo("errors.order.itemSoldOut"));
  }

  [Test]
  public async Task PlaceAsync_AnArticleWhoseOnlyStationIsSwitchedOff_IsRefusedAsAGuard()
  {
    var kitchen = await _fixture.DbContext.Stations.SingleAsync(station => station.Id == _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);
    kitchen.IsActive = false;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    ErrorOr<PlacedOrderView> result = await PlaceAsync(RequestWith(ItemFor(_seeded.SausageItemId)));

    await AssertNothingWasNumberedOrStoredAsync();
    Assert.That(result.RefusalMessageKey(), Is.EqualTo("errors.order.cannotBeProcessed"));
  }

  [Test]
  public async Task PlaceAsync_TheChosenStationWasSwitchedOff_IsRefusedAsSoldOut()
  {
    await AssignAsync(_seeded.SausageItemId, _seeded.BarStationId, false);
    var bar = await _fixture.DbContext.Stations.SingleAsync(station => station.Id == _seeded.BarStationId, TestContext.CurrentContext.CancellationToken);
    bar.IsActive = false;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    ErrorOr<PlacedOrderView> result = await PlaceAsync(RequestWith(ItemFor(_seeded.SausageItemId, _seeded.BarStationId)));

    await AssertNothingWasNumberedOrStoredAsync();
    Assert.That(result.RefusalMessageKey(), Is.EqualTo("errors.order.itemSoldOut"));
  }

  [Test]
  public async Task PlaceAsync_OneGoodAndOneRefusedLine_StoresNothingOfTheOrder()
  {
    ErrorOr<PlacedOrderView> result = await PlaceAsync(RequestWith(ItemFor(_seeded.SausageItemId), ItemFor(Guid.NewGuid())));

    await AssertNothingWasNumberedOrStoredAsync();
    Assert.That(result.IsError, Is.True);
  }

  [Test]
  public async Task PlaceAsync_TheOrderLeavesLessThanOnePortionOfAnIngredient_StoresTheArticleAsSoldOut()
  {
    await AddLimitedIngredientAsync(_seeded.SausageItemId, 30, 20);

    ErrorOr<PlacedOrderView> result = await PlaceAsync(RequestWith(ItemFor(_seeded.SausageItemId)));
    _fixture.DbContext.ChangeTracker.Clear();

    var sausageRow = await _fixture.DbContext.FestivalCatalogItems.SingleAsync(menuRow => menuRow.CatalogItemId == _seeded.SausageItemId, TestContext.CurrentContext.CancellationToken);
    var lemonadeRow = await _fixture.DbContext.FestivalCatalogItems.SingleAsync(menuRow => menuRow.CatalogItemId == _seeded.LemonadeItemId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(result.IsSuccess, Is.True);
                      Assert.That(sausageRow.IsAvailable, Is.False);
                      Assert.That(lemonadeRow.IsAvailable, Is.True);
                    });
  }

  private async Task<ErrorOr<PlacedOrderView>> PlaceAsync(PlaceOrderRequest request)
  {
    return await _service.PlaceAsync(request, _seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);
  }

  private PlaceOrderRequest RequestWith(params OrderItemRequest[] items)
  {
    return RequestWith(Guid.NewGuid(), items);
  }

  private PlaceOrderRequest RequestWith(Guid clientOrderId, params OrderItemRequest[] items)
  {
    return new()
           {
             ClientOrderId = clientOrderId,
             TableId = _tableId,
             Items = items,
             DeliveryModes = []
           };
  }

  private OrderItemRequest ItemFor(Guid catalogItemId, Guid? stationId = null, int unitPriceCents = 350, string? note = null)
  {
    return new()
           {
             CatalogItemId = catalogItemId,
             UnitPriceCents = unitPriceCents,
             Note = note,
             StationId = stationId
           };
  }

  private string Serialized(PlacedOrderView view)
  {
    return JsonSerializer.Serialize(view);
  }

  private void HandOut(OrderItem item)
  {
    new OrderItemLifecycle().AppendStatus(item, OrderItemState.HandedOut, new(null, _seeded.KitchenStationId), DateTime.UtcNow);
  }

  private async Task<Order> StoredOrderAsync()
  {
    return await _fixture.DbContext.Orders.AsNoTracking().Include(order => order.StationOrders).ThenInclude(stationOrder => stationOrder.Items).SingleAsync(TestContext.CurrentContext.CancellationToken);
  }

  private Guid StoredTableId()
  {
    return _fixture.DbContext.Orders.AsNoTracking().Select(order => order.TableId).Single();
  }

  private async Task AssertNothingWasNumberedOrStoredAsync()
  {
    using var verification = _fixture.CreateContext();
    var orderCount = await verification.Orders.CountAsync(TestContext.CurrentContext.CancellationToken);
    var nextOrderNumber = await verification.Festivals.Where(festival => festival.Id == _seeded.FestivalId).Select(festival => festival.NextOrderNumber).SingleAsync(TestContext.CurrentContext.CancellationToken);
    List<int> nextStationNumbers = await verification.FestivalStations.Where(link => link.FestivalId == _seeded.FestivalId).Select(link => link.NextStationOrderNumber).ToListAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(orderCount, Is.Zero);
                      Assert.That(nextOrderNumber, Is.EqualTo(1));
                      Assert.That(nextStationNumbers, Is.All.EqualTo(1));
                    });
  }

  private async Task<int> NextStationOrderNumberAsync(Guid stationId)
  {
    return await _fixture.DbContext.FestivalStations.AsNoTracking().Where(link => link.StationId == stationId).Select(link => link.NextStationOrderNumber).SingleAsync(TestContext.CurrentContext.CancellationToken);
  }

  private async Task SetNextStationOrderNumberAsync(Guid stationId, int nextNumber)
  {
    var link = await _fixture.DbContext.FestivalStations.SingleAsync(candidate => candidate.StationId == stationId, TestContext.CurrentContext.CancellationToken);
    link.NextStationOrderNumber = nextNumber;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }

  private async Task AssignAsync(Guid catalogItemId, Guid stationId, bool replaceExisting, Guid? festivalId = null)
  {
    if (replaceExisting)
      _fixture.DbContext.ItemStationAssignments.RemoveRange(_fixture.DbContext.ItemStationAssignments.Where(assignment => assignment.CatalogItemId == catalogItemId));

    _fixture.DbContext.ItemStationAssignments.Add(new()
                                                  {
                                                    Id = Guid.NewGuid(),
                                                    FestivalId = festivalId ?? _seeded.FestivalId,
                                                    CatalogItemId = catalogItemId,
                                                    StationId = stationId
                                                  });
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }

  private async Task<Guid> AddFestivalAsync()
  {
    var festivalId = Guid.NewGuid();
    _fixture.DbContext.Festivals.Add(new()
                                     {
                                       Id = festivalId,
                                       Name = "Herbstfest",
                                       StartsAtUtc = new(2030, 10, 1, 12, 0, 0, DateTimeKind.Utc),
                                       EndsAtUtc = new(2030, 10, 2, 12, 0, 0, DateTimeKind.Utc),
                                       NextOrderNumber = 1,
                                       IsHidden = false
                                     });
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return festivalId;
  }

  private async Task AddLimitedIngredientAsync(Guid catalogItemId, double availableAmount, double portionAmount)
  {
    Unit unit = new()
                {
                  Id = Guid.NewGuid(),
                  Name = "Portion",
                  IsActive = true
                };
    Ingredient mustard = new()
                         {
                           Id = Guid.NewGuid(),
                           Name = "Senf",
                           UnitId = unit.Id,
                           Unit = unit,
                           IsActive = true
                         };
    mustard.FestivalIngredients.Add(new()
                                    {
                                      Id = Guid.NewGuid(),
                                      FestivalId = _seeded.FestivalId,
                                      IngredientId = mustard.Id,
                                      AvailableAmount = availableAmount
                                    });
    _fixture.DbContext.Units.Add(unit);
    _fixture.DbContext.Ingredients.Add(mustard);
    _fixture.DbContext.CatalogItemIngredients.Add(new()
                                                  {
                                                    Id = Guid.NewGuid(),
                                                    CatalogItemId = catalogItemId,
                                                    IngredientId = mustard.Id,
                                                    Amount = portionAmount
                                                  });
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }
}
