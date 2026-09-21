using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class OrderTotalCalculatorTest
{
  [SetUp]
  public void SetUp()
  {
    _calculator = new();
  }

  private readonly DateTime _placedAtUtc = new(2026, 9, 5, 19, 5, 0, DateTimeKind.Utc);

  private OrderTotalCalculator _calculator = null!;

  [Test]
  public void SumTotalCents_NullOrder_ThrowsArgumentNullException()
  {
    Assert.That(() => _calculator.SumTotalCents(null!), Throws.ArgumentNullException);
  }

  [Test]
  public void SumTotalCents_AnOrderWithoutItems_CountsNothing()
  {
    Assert.That(_calculator.SumTotalCents(OrderWith()), Is.Zero);
  }

  [Test]
  public void SumTotalCents_SeveralItems_AddsThePricesThePhoneDisplayed()
  {
    Assert.That(_calculator.SumTotalCents(OrderWith(350, 400, 250)), Is.EqualTo(1000));
  }

  [Test]
  public void SumTotalCents_ItemsAtTwoStations_AddsUpBothStationOrders()
  {
    var order = OrderWith(350);
    var second = StationOrderWith(order.Id, 400);
    order.StationOrders.Add(second);

    Assert.That(_calculator.SumTotalCents(order), Is.EqualTo(750));
  }

  private Order OrderWith(params int[] unitPricesCents)
  {
    Order order = new()
                  {
                    Id = Guid.NewGuid(),
                    ClientOrderId = Guid.NewGuid(),
                    FestivalId = Guid.NewGuid(),
                    GlobalOrderNumber = 7,
                    StaffMemberId = Guid.NewGuid(),
                    TableName = "Tisch 12",
                    CreatedAtUtc = _placedAtUtc
                  };

    if (unitPricesCents.Length > 0)
      order.StationOrders.Add(StationOrderWith(order.Id, unitPricesCents));

    return order;
  }

  private StationOrder StationOrderWith(Guid orderId, params int[] unitPricesCents)
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
}
