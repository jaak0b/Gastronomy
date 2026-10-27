using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class OrderServiceTest
{
  private readonly OrderItemLifecycle _lifecycle = new();
  private readonly OrderService _service = new();

  private readonly DateTime _placedAtUtc = new(2026, 9, 5, 19, 5, 0, DateTimeKind.Utc);
  private readonly DateTime _handedOutAtUtc = new(2026, 9, 5, 19, 30, 0, DateTimeKind.Utc);

  [Test]
  public void StatusOf_NothingHandedOut_IsOpen()
  {
    Assert.That(_service.StatusOf(OrderWithItems(2, 0)), Is.EqualTo(OrderStatus.Open));
  }

  [Test]
  public void StatusOf_SomeItemsHandedOut_IsPartiallyFulfilled()
  {
    Assert.That(_service.StatusOf(OrderWithItems(3, 1)), Is.EqualTo(OrderStatus.PartiallyFulfilled));
  }

  [Test]
  public void StatusOf_EveryItemHandedOut_IsFulfilled()
  {
    Assert.That(_service.StatusOf(OrderWithItems(2, 2)), Is.EqualTo(OrderStatus.Fulfilled));
  }

  [Test]
  public void StatusOf_AnOrderWithoutItems_IsFulfilled()
  {
    Assert.That(_service.StatusOf(OrderWithItems(0, 0)), Is.EqualTo(OrderStatus.Fulfilled));
  }

  [Test]
  public void StatusOf_ItemsSpreadOverTwoStationOrders_CountsEveryStationOrder()
  {
    var order = OrderWithItems(1, 1);
    order.StationOrders.Add(StationOrderWithItems(order.Id, 1, 0));

    Assert.That(_service.StatusOf(order), Is.EqualTo(OrderStatus.PartiallyFulfilled));
  }

  [Test]
  public void ShareOfFlaggedItems_NoItemFlagged_IsNone()
  {
    Assert.That(_service.ShareOfFlaggedItems([
                                               false,
                                               false
                                             ]),
                Is.EqualTo(ShareState.None));
  }

  [Test]
  public void ShareOfFlaggedItems_SomeItemsFlagged_IsSome()
  {
    Assert.That(_service.ShareOfFlaggedItems([
                                               true,
                                               false,
                                               false
                                             ]),
                Is.EqualTo(ShareState.Some));
  }

  [Test]
  public void ShareOfFlaggedItems_EveryItemFlagged_IsAll()
  {
    Assert.That(_service.ShareOfFlaggedItems([
                                               true,
                                               true
                                             ]),
                Is.EqualTo(ShareState.All));
  }

  [Test]
  public void ShareOfFlaggedItems_NoItems_IsAll()
  {
    Assert.That(_service.ShareOfFlaggedItems([]), Is.EqualTo(ShareState.All));
  }

  [Test]
  public void ShareOfFlaggedItems_NullFlags_ThrowsArgumentNullException()
  {
    Assert.That(() => _service.ShareOfFlaggedItems(null!), Throws.ArgumentNullException);
  }

  [Test]
  public void TotalCentsOf_AnOrderWithoutItems_CountsNothing()
  {
    Assert.That(_service.TotalCentsOf(OrderWithPrices()), Is.Zero);
  }

  [Test]
  public void TotalCentsOf_SeveralItems_AddsThePricesThePhoneDisplayed()
  {
    Assert.That(_service.TotalCentsOf(OrderWithPrices(350, 400, 250)), Is.EqualTo(1000));
  }

  [Test]
  public void TotalCentsOf_ItemsAtTwoStations_AddsUpBothStationOrders()
  {
    var order = OrderWithPrices(350);
    order.StationOrders.Add(StationOrderWithPrices(order.Id, 400));

    Assert.That(_service.TotalCentsOf(order), Is.EqualTo(750));
  }

  [Test]
  public void StatusOf_EveryItemCancelled_IsFulfilled()
  {
    var order = OrderWithItems(2, 0);
    CancelItems(order, 2);

    Assert.That(_service.StatusOf(order), Is.EqualTo(OrderStatus.Fulfilled));
  }

  [Test]
  public void StatusOf_TheOnlyItemNotCancelledIsHandedOut_IsFulfilled()
  {
    Assert.That(_service.StatusOf(OrderWithOneHandedOutAndOthersCancelled()), Is.EqualTo(OrderStatus.Fulfilled));
  }

  [Test]
  public void StatusOf_AnOpenItemBesideACancelledOne_IsOpen()
  {
    var order = OrderWithItems(2, 0);
    CancelItems(order, 1);

    Assert.That(_service.StatusOf(order), Is.EqualTo(OrderStatus.Open));
  }

  [Test]
  public void TotalCentsOf_ACancelledItem_LeavesItsPriceOut()
  {
    var order = OrderWithPrices(350, 400, 250);
    CancelItems(order, 1);

    Assert.That(_service.TotalCentsOf(order), Is.EqualTo(650));
  }

  [Test]
  public void HandOutProgressOf_EveryItemCancelled_IsAll()
  {
    Assert.That(_service.HandOutProgressOf([
                                             new(OrderItemState.Cancelled, false),
                                             new(OrderItemState.CancellationAcknowledged, false)
                                           ]),
                Is.EqualTo(ShareState.All));
  }

  [Test]
  public void HandOutProgressOf_AHandedOutItemBesideACancelledOne_IsAll()
  {
    Assert.That(_service.HandOutProgressOf([
                                             new(OrderItemState.HandedOut, false),
                                             new(OrderItemState.Cancelled, false)
                                           ]),
                Is.EqualTo(ShareState.All));
  }

  [Test]
  public void HandOutProgressOf_AnOpenItemBesideAHandedOutOne_IsSome()
  {
    Assert.That(_service.HandOutProgressOf([
                                             new(OrderItemState.HandedOut, false),
                                             new(OrderItemState.Open, false)
                                           ]),
                Is.EqualTo(ShareState.Some));
  }

  [Test]
  public void HandOutProgressOf_NullItems_ThrowsArgumentNullException()
  {
    Assert.That(() => _service.HandOutProgressOf(null!), Throws.ArgumentNullException);
  }

  [Test]
  public void SettlementProgressOf_EveryItemCancelled_IsAll()
  {
    Assert.That(_service.SettlementProgressOf([
                                                new(OrderItemState.Cancelled, false),
                                                new(OrderItemState.CancellationAcknowledged, false)
                                              ]),
                Is.EqualTo(ShareState.All));
  }

  [Test]
  public void SettlementProgressOf_ASettledItemBesideAnUnsettledCancelledOne_IsAll()
  {
    Assert.That(_service.SettlementProgressOf([
                                                new(OrderItemState.Open, true),
                                                new(OrderItemState.Cancelled, false)
                                              ]),
                Is.EqualTo(ShareState.All));
  }

  [Test]
  public void SettlementProgressOf_AnUnsettledItemBesideASettledOne_IsSome()
  {
    Assert.That(_service.SettlementProgressOf([
                                                new(OrderItemState.Open, true),
                                                new(OrderItemState.HandedOut, false)
                                              ]),
                Is.EqualTo(ShareState.Some));
  }

  [Test]
  public void SettlementProgressOf_NullItems_ThrowsArgumentNullException()
  {
    Assert.That(() => _service.SettlementProgressOf(null!), Throws.ArgumentNullException);
  }

  private Order OrderWithOneHandedOutAndOthersCancelled()
  {
    var order = OrderWithItems(3, 1);
    List<OrderItem> items = order.StationOrders.SelectMany(stationOrder => stationOrder.Items).ToList();

    foreach (var item in items.Skip(1))
      _lifecycle.AppendStatus(item, OrderItemState.Cancelled, new(order.StaffMemberId, null), _handedOutAtUtc.AddMinutes(1));

    return order;
  }

  private void CancelItems(Order order, int cancelledItemCount)
  {
    foreach (var item in order.StationOrders.SelectMany(stationOrder => stationOrder.Items).Take(cancelledItemCount))
      _lifecycle.AppendStatus(item, OrderItemState.Cancelled, new(order.StaffMemberId, null), _handedOutAtUtc.AddMinutes(1));
  }

  private Order OrderWithItems(int itemCount, int fulfilledItemCount)
  {
    var order = EmptyOrder();

    if (itemCount > 0)
      order.StationOrders.Add(StationOrderWithItems(order.Id, itemCount, fulfilledItemCount));

    return order;
  }

  private Order OrderWithPrices(params int[] unitPricesCents)
  {
    var order = EmptyOrder();

    if (unitPricesCents.Length > 0)
      order.StationOrders.Add(StationOrderWithPrices(order.Id, unitPricesCents));

    return order;
  }

  private Order EmptyOrder()
  {
    return new()
           {
             Id = Guid.NewGuid(),
             ClientOrderId = Guid.NewGuid(),
             FestivalId = Guid.NewGuid(),
             GlobalOrderNumber = 7,
             StaffMemberId = Guid.NewGuid(),
             TableId = Guid.NewGuid(),
             CreatedAtUtc = _placedAtUtc
           };
  }

  private StationOrder StationOrderWithItems(Guid orderId, int itemCount, int fulfilledItemCount)
  {
    var stationOrder = EmptyStationOrder(orderId);

    for (var index = 0; index < itemCount; index++)
    {
      OrderItem item = new()
                       {
                         Id = Guid.NewGuid(),
                         StationOrderId = stationOrder.Id,
                         CatalogItemId = Guid.NewGuid(),
                         ItemName = "Bratwurst",
                         UnitPriceCents = 350
                       };

      _lifecycle.AppendStatus(item, OrderItemState.Open, new(Guid.NewGuid(), null), _placedAtUtc);
      _lifecycle.PointAtFirstStatus(item);

      if (index < fulfilledItemCount)
        _lifecycle.AppendStatus(item, OrderItemState.HandedOut, new(null, stationOrder.StationId), _handedOutAtUtc);

      stationOrder.Items.Add(item);
    }

    return stationOrder;
  }

  private StationOrder StationOrderWithPrices(Guid orderId, params int[] unitPricesCents)
  {
    var stationOrder = EmptyStationOrder(orderId);

    foreach (var unitPriceCents in unitPricesCents)
    {
      OrderItem item = new()
                       {
                         Id = Guid.NewGuid(),
                         StationOrderId = stationOrder.Id,
                         CatalogItemId = Guid.NewGuid(),
                         ItemName = "Bratwurst",
                         UnitPriceCents = unitPriceCents
                       };

      _lifecycle.AppendStatus(item, OrderItemState.Open, new(Guid.NewGuid(), null), _placedAtUtc);
      _lifecycle.PointAtFirstStatus(item);
      stationOrder.Items.Add(item);
    }

    return stationOrder;
  }

  private StationOrder EmptyStationOrder(Guid orderId)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             OrderId = orderId,
             FestivalId = Guid.NewGuid(),
             StationId = Guid.NewGuid(),
             StationOrderNumber = 1,
             DeliveryMode = DeliveryMode.Together
           };
  }
}
