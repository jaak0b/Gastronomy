using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Queries;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Queries;

[TestFixture]
public sealed class OrderQueriesTest
{
  private readonly OrderItemLifecycle _lifecycle = new();

  private readonly DateTime _placedAtUtc = new(2026, 9, 5, 19, 5, 0, DateTimeKind.Utc);
  private readonly DateTime _handedOutAtUtc = new(2026, 9, 5, 19, 30, 0, DateTimeKind.Utc);
  private readonly DateTime _settledAtUtc = new(2026, 9, 5, 19, 40, 0, DateTimeKind.Utc);

  [Test]
  public void Status_NothingHandedOut_IsOpen()
  {
    Assert.That(OrderWithItems(2, 0).Status(), Is.EqualTo(OrderStatus.Open));
  }

  [Test]
  public void Status_SomeItemsHandedOut_IsPartiallyFulfilled()
  {
    Assert.That(OrderWithItems(3, 1).Status(), Is.EqualTo(OrderStatus.PartiallyFulfilled));
  }

  [Test]
  public void Status_EveryItemHandedOut_IsFulfilled()
  {
    Assert.That(OrderWithItems(2, 2).Status(), Is.EqualTo(OrderStatus.Fulfilled));
  }

  [Test]
  public void Status_AnOrderWithoutItems_IsFulfilled()
  {
    Assert.That(OrderWithItems(0, 0).Status(), Is.EqualTo(OrderStatus.Fulfilled));
  }

  [Test]
  public void Status_ItemsSpreadOverTwoStationOrders_CountsEveryStationOrder()
  {
    var order = OrderWithItems(1, 1);
    order.StationOrders.Add(StationOrderWithItems(order.Id, 1, 0));

    Assert.That(order.Status(), Is.EqualTo(OrderStatus.PartiallyFulfilled));
  }

  [Test]
  public void Status_EveryItemCancelled_IsFulfilled()
  {
    var order = OrderWithItems(2, 0);
    CancelItems(order, 2);

    Assert.That(order.Status(), Is.EqualTo(OrderStatus.Fulfilled));
  }

  [Test]
  public void Status_TheOnlyItemNotCancelledIsHandedOut_IsFulfilled()
  {
    var order = OrderWithItems(3, 1);
    CancelItems(order, 2, 1);

    Assert.That(order.Status(), Is.EqualTo(OrderStatus.Fulfilled));
  }

  [Test]
  public void Status_AnOpenItemBesideACancelledOne_IsOpen()
  {
    var order = OrderWithItems(2, 0);
    CancelItems(order, 1);

    Assert.That(order.Status(), Is.EqualTo(OrderStatus.Open));
  }

  [Test]
  public void IsPartlyHandedOut_AHandedOutItemBesideAnOpenOne_IsTrue()
  {
    var order = OrderWithItems(2, 1);

    Assert.Multiple(() =>
                    {
                      Assert.That(order.HasItemsToHandOut(), Is.True);
                      Assert.That(order.IsPartlyHandedOut(), Is.True);
                    });
  }

  [Test]
  public void IsPartlyHandedOut_AHandedOutItemBesideACancelledOne_IsFalseBecauseNothingIsLeftToHandOut()
  {
    var order = OrderWithItems(2, 1);
    CancelItems(order, 1, 1);

    Assert.Multiple(() =>
                    {
                      Assert.That(order.HasItemsToHandOut(), Is.False);
                      Assert.That(order.IsPartlyHandedOut(), Is.False);
                    });
  }

  [Test]
  public void IsPartlyHandedOut_OnlyOpenItems_IsFalse()
  {
    var order = OrderWithItems(2, 0);

    Assert.Multiple(() =>
                    {
                      Assert.That(order.HasItemsToHandOut(), Is.True);
                      Assert.That(order.IsPartlyHandedOut(), Is.False);
                    });
  }

  [Test]
  public void IsPartlySettled_ASettledItemBesideAnUnsettledOne_IsTrue()
  {
    var order = OrderWithItems(2, 0);
    SettleItems(order, 1);

    Assert.Multiple(() =>
                    {
                      Assert.That(order.HasItemsToSettle(), Is.True);
                      Assert.That(order.IsPartlySettled(), Is.True);
                    });
  }

  [Test]
  public void IsPartlySettled_ASettledItemBesideAnUnsettledCancelledOne_IsFalseBecauseNothingIsLeftToSettle()
  {
    var order = OrderWithItems(2, 0);
    SettleItems(order, 1);
    CancelItems(order, 1, 1);

    Assert.Multiple(() =>
                    {
                      Assert.That(order.HasItemsToSettle(), Is.False);
                      Assert.That(order.IsPartlySettled(), Is.False);
                    });
  }

  [Test]
  public void IsPartlySettled_ASettledCancelledItemBesideAnUnsettledOne_IsFalseBecauseTheCancelledOneIsLeftOut()
  {
    var order = OrderWithItems(2, 0);
    SettleItems(order, 1);
    CancelItems(order, 1);

    Assert.Multiple(() =>
                    {
                      Assert.That(order.HasItemsToSettle(), Is.True);
                      Assert.That(order.IsPartlySettled(), Is.False);
                    });
  }

  [Test]
  public void HasItemsToSettle_EveryItemCancelled_IsFalse()
  {
    var order = OrderWithItems(2, 0);
    CancelItems(order, 2);

    Assert.That(order.HasItemsToSettle(), Is.False);
  }

  private void CancelItems(Order order, int cancelledItemCount, int skippedItemCount = 0)
  {
    foreach (var item in order.StationOrders.SelectMany(stationOrder => stationOrder.Items).Skip(skippedItemCount).Take(cancelledItemCount))
      _lifecycle.AppendStatus(item, OrderItemState.Cancelled, new(order.StaffMemberId, null), _handedOutAtUtc.AddMinutes(1));
  }

  private void SettleItems(Order order, int settledItemCount)
  {
    foreach (var item in order.StationOrders.SelectMany(stationOrder => stationOrder.Items).Take(settledItemCount))
      item.SettledAtUtc = _settledAtUtc;
  }

  private Order OrderWithItems(int itemCount, int handedOutItemCount)
  {
    Order order = new()
                  {
                    Id = Guid.NewGuid(),
                    ClientOrderId = Guid.NewGuid(),
                    FestivalId = Guid.NewGuid(),
                    GlobalOrderNumber = 7,
                    StaffMemberId = Guid.NewGuid(),
                    TableId = Guid.NewGuid(),
                    CreatedAtUtc = _placedAtUtc
                  };

    if (itemCount > 0)
      order.StationOrders.Add(StationOrderWithItems(order.Id, itemCount, handedOutItemCount));

    return order;
  }

  private StationOrder StationOrderWithItems(Guid orderId, int itemCount, int handedOutItemCount)
  {
    StationOrder stationOrder = new()
                                {
                                  Id = Guid.NewGuid(),
                                  OrderId = orderId,
                                  FestivalId = Guid.NewGuid(),
                                  StationId = Guid.NewGuid(),
                                  StationOrderNumber = 1,
                                  DeliveryMode = DeliveryMode.Together
                                };

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

      if (index < handedOutItemCount)
        _lifecycle.AppendStatus(item, OrderItemState.HandedOut, new(null, stationOrder.StationId), _handedOutAtUtc);

      stationOrder.Items.Add(item);
    }

    return stationOrder;
  }
}
