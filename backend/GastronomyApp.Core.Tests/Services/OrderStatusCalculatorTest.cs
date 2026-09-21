using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class OrderStatusCalculatorTest
{
  [SetUp]
  public void SetUp()
  {
    _calculator = new();
  }

  private readonly DateTime _handedOutAtUtc = new(2026, 9, 5, 19, 30, 0, DateTimeKind.Utc);

  private OrderStatusCalculator _calculator = new();

  [Test]
  public void Calculate_NullOrder_ThrowsArgumentNullException()
  {
    Assert.That(() => _calculator.Calculate(null!), Throws.ArgumentNullException);
  }

  [Test]
  public void Calculate_NothingHandedOut_IsOpen()
  {
    Assert.That(_calculator.Calculate(OrderWith(2, 0)), Is.EqualTo(OrderStatus.Open));
  }

  [Test]
  public void Calculate_SomeItemsHandedOut_IsPartiallyFulfilled()
  {
    Assert.That(_calculator.Calculate(OrderWith(3, 1)), Is.EqualTo(OrderStatus.PartiallyFulfilled));
  }

  [Test]
  public void Calculate_EveryItemHandedOut_IsFulfilled()
  {
    Assert.That(_calculator.Calculate(OrderWith(2, 2)), Is.EqualTo(OrderStatus.Fulfilled));
  }

  [Test]
  public void Calculate_AnOrderWithoutItems_IsOpen()
  {
    Assert.That(_calculator.Calculate(OrderWith(0, 0)), Is.EqualTo(OrderStatus.Open));
  }

  [Test]
  public void Calculate_ItemsSpreadOverTwoStationOrders_CountsEveryStationOrder()
  {
    var order = OrderWith(1, 1);
    order.StationOrders.Add(StationOrderWith(order.Id, 1, 0));

    Assert.That(_calculator.Calculate(order), Is.EqualTo(OrderStatus.PartiallyFulfilled));
  }

  private Order OrderWith(int itemCount, int fulfilledItemCount)
  {
    Order order = new()
                  {
                    Id = Guid.NewGuid(),
                    ClientOrderId = Guid.NewGuid(),
                    FestivalId = Guid.NewGuid(),
                    GlobalOrderNumber = 7,
                    StaffMemberId = Guid.NewGuid(),
                    TableName = "Tisch 12",
                    CreatedAtUtc = _handedOutAtUtc
                  };

    if (itemCount > 0)
      order.StationOrders.Add(StationOrderWith(order.Id, itemCount, fulfilledItemCount));

    return order;
  }

  private StationOrder StationOrderWith(Guid orderId, int itemCount, int fulfilledItemCount)
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
}
