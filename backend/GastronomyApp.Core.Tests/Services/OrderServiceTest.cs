using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class OrderServiceTest
{
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
  public void StatusOf_AnOrderWithoutItems_IsOpen()
  {
    Assert.That(_service.StatusOf(OrderWithItems(0, 0)), Is.EqualTo(OrderStatus.Open));
  }

  [Test]
  public void StatusOf_ItemsSpreadOverTwoStationOrders_CountsEveryStationOrder()
  {
    var order = OrderWithItems(1, 1);
    order.StationOrders.Add(StationOrderWithItems(order.Id, 1, 0));

    Assert.That(_service.StatusOf(order), Is.EqualTo(OrderStatus.PartiallyFulfilled));
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
