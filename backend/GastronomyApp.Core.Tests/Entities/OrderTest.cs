using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Tests.Entities;

[TestFixture]
public sealed class OrderTest
{
  private readonly DateTime _placedAtUtc = new(2026, 9, 5, 19, 5, 0, DateTimeKind.Utc);
  private readonly DateTime _handedOutAtUtc = new(2026, 9, 5, 19, 30, 0, DateTimeKind.Utc);

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
  public void Status_AnOrderWithoutItems_IsOpen()
  {
    Assert.That(OrderWithItems(0, 0).Status(), Is.EqualTo(OrderStatus.Open));
  }

  [Test]
  public void Status_ItemsSpreadOverTwoStationOrders_CountsEveryStationOrder()
  {
    var order = OrderWithItems(1, 1);
    order.StationOrders.Add(StationOrderWithItems(order.Id, 1, 0));

    Assert.That(order.Status(), Is.EqualTo(OrderStatus.PartiallyFulfilled));
  }

  [Test]
  public void TotalCents_AnOrderWithoutItems_CountsNothing()
  {
    Assert.That(OrderWithPrices().TotalCents(), Is.Zero);
  }

  [Test]
  public void TotalCents_SeveralItems_AddsThePricesThePhoneDisplayed()
  {
    Assert.That(OrderWithPrices(350, 400, 250).TotalCents(), Is.EqualTo(1000));
  }

  [Test]
  public void TotalCents_ItemsAtTwoStations_AddsUpBothStationOrders()
  {
    var order = OrderWithPrices(350);
    order.StationOrders.Add(StationOrderWithPrices(order.Id, 400));

    Assert.That(order.TotalCents(), Is.EqualTo(750));
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
             TableName = "Tisch 12",
             CreatedAtUtc = _placedAtUtc
           };
  }

  private StationOrder StationOrderWithItems(Guid orderId, int itemCount, int fulfilledItemCount)
  {
    var stationOrder = EmptyStationOrder(orderId);

    for (var index = 0; index < itemCount; index++)
    {
      stationOrder.Items.Add(new()
                             {
                               Id = Guid.NewGuid(),
                               StationOrderId = stationOrder.Id,
                               CatalogItemId = Guid.NewGuid(),
                               ItemName = "Bratwurst",
                               UnitPriceCents = 350,
                               FulfilledAtUtc = index < fulfilledItemCount ? _handedOutAtUtc : null
                             });
    }

    return stationOrder;
  }

  private StationOrder StationOrderWithPrices(Guid orderId, params int[] unitPricesCents)
  {
    var stationOrder = EmptyStationOrder(orderId);

    foreach (var unitPriceCents in unitPricesCents)
      stationOrder.Items.Add(new()
                             {
                               Id = Guid.NewGuid(),
                               StationOrderId = stationOrder.Id,
                               CatalogItemId = Guid.NewGuid(),
                               ItemName = "Bratwurst",
                               UnitPriceCents = unitPriceCents
                             });

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
