using FakeItEasy;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class PlacedOrderReaderTest
{
  [SetUp]
  public void SetUp()
  {
    _orderRepository = A.Fake<IOrderRepository>();
    A.CallTo(() => _orderRepository.FindPlacedAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<PlacedOrder?>(null));

    _reader = new(_orderRepository, new(), new());
  }

  private readonly DateTime _placedAtUtc = new(2026, 9, 5, 19, 5, 0, DateTimeKind.Utc);
  private readonly Guid _orderId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

  private IOrderRepository _orderRepository = null!;
  private PlacedOrderReader _reader = null!;

  [Test]
  public async Task FindAsync_AnOrderThatIsNotThere_ReturnsNothing()
  {
    Assert.That(await _reader.FindAsync(_orderId, CancellationToken.None), Is.Null);
  }

  [Test]
  public async Task FindAsync_AnOrderNoStationHasHandedOutYet_ReportsItAsOpenWithItsTotal()
  {
    GivenTheOrderHolds(Item(350, false), Item(400, false));

    var report = (await _reader.FindAsync(_orderId, CancellationToken.None))!;

    Assert.Multiple(() =>
                    {
                      Assert.That(report.Status, Is.EqualTo(OrderStatus.Open));
                      Assert.That(report.TotalCents, Is.EqualTo(750));
                      Assert.That(report.Order.OrderId, Is.EqualTo(_orderId));
                    });
  }

  [Test]
  public async Task FindAsync_AnOrderWhereOneItemWasHandedOut_ReportsItAsPartiallyFulfilled()
  {
    GivenTheOrderHolds(Item(350, true), Item(400, false));

    var report = (await _reader.FindAsync(_orderId, CancellationToken.None))!;

    Assert.That(report.Status, Is.EqualTo(OrderStatus.PartiallyFulfilled));
  }

  [Test]
  public async Task FindAsync_AnOrderWhereEveryItemWasHandedOut_ReportsItAsFulfilled()
  {
    GivenTheOrderHolds(Item(350, true), Item(400, true));

    var report = (await _reader.FindAsync(_orderId, CancellationToken.None))!;

    Assert.That(report.Status, Is.EqualTo(OrderStatus.Fulfilled));
  }

  private void GivenTheOrderHolds(params PlacedOrderItem[] items)
  {
    PlacedOrder placedOrder = new()
                              {
                                OrderId = _orderId,
                                GlobalOrderNumber = 7,
                                CreatedAtUtc = _placedAtUtc,
                                StationOrders =
                                [
                                  new()
                                  {
                                    StationOrderId = Guid.NewGuid(),
                                    StationId = Guid.NewGuid(),
                                    StationName = "Kueche",
                                    StationOrderNumber = 3,
                                    DeliveryMode = DeliveryMode.Together,
                                    Items = items
                                  }
                                ]
                              };

    A.CallTo(() => _orderRepository.FindPlacedAsync(_orderId, A<CancellationToken>._)).Returns(Task.FromResult<PlacedOrder?>(placedOrder));
  }

  private PlacedOrderItem Item(int unitPriceCents, bool isFulfilled)
  {
    return new()
           {
             OrderItemId = Guid.NewGuid(),
             UnitPriceCents = unitPriceCents,
             IsFulfilled = isFulfilled
           };
  }
}
