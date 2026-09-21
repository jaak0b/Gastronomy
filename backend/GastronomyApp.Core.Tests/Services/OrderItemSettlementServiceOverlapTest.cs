using ErrorOr;
using FakeItEasy;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Contracts.OpenItems;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using Microsoft.Extensions.Time.Testing;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class OrderItemSettlementServiceOverlapTest
{
  [SetUp]
  public void SetUp()
  {
    var timeProvider = new FakeTimeProvider(new(_now));

    _service = new(A.Fake<IOpenItemRepository>(), new(A.Fake<IFestivalRepository>(), new(), timeProvider), A.Fake<ITransactionRunner>(), timeProvider);
  }

  private readonly DateTime _now = new(2026, 9, 5, 20, 15, 0, DateTimeKind.Utc);
  private readonly DateTime _earlier = new(2026, 9, 5, 19, 5, 0, DateTimeKind.Utc);
  private readonly Guid _collectingWaiter = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
  private readonly Guid _anotherWaiter = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

  private OrderItemSettlementService _service = null!;

  [Test]
  public void Settle_AStaleSelectionWhereAColleagueTookOneLine_LeavesTheColleagueLineUntouched()
  {
    var firstBeer = OpenItem(350);
    var secondBeer = OpenItem(350);
    var hotdog = OpenItem(400);
    _service.MarkSettled(firstBeer, 0, "Kapelle", _anotherWaiter, _earlier);

    ErrorOr<SettlementResult> settlement = _service.Settle([
                                                                               Line(firstBeer, 350),
                                                                               Line(secondBeer, 350),
                                                                               Line(hotdog, 400)
                                                                             ],
                                                                             _collectingWaiter,
                                                                             AtOneTable(firstBeer, secondBeer, hotdog),
                                                                             _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.True);
                      Assert.That(settlement.Value.NewlySettled, Has.Count.EqualTo(2));
                      Assert.That(settlement.Value.Reapplied, Is.Empty);
                      Assert.That(settlement.Value.AlreadySettledByOthers, Has.Count.EqualTo(1));
                      Assert.That(firstBeer.ChargedPriceCents, Is.Zero);
                      Assert.That(firstBeer.PaymentNotice, Is.EqualTo("Kapelle"));
                      Assert.That(firstBeer.SettledAtUtc, Is.EqualTo(_earlier));
                      Assert.That(firstBeer.SettledByStaffMemberId, Is.EqualTo(_anotherWaiter));
                    });
  }

  [Test]
  public void Settle_AStaleSelectionWithOneLineAlreadyTaken_ChargesTheOpenLinesExactlyThePricesSent()
  {
    var firstBeer = OpenItem(350);
    var secondBeer = OpenItem(350);
    var hotdog = OpenItem(400);
    _service.MarkSettled(firstBeer, 0, "Kapelle", _anotherWaiter, _earlier);

    ErrorOr<SettlementResult> settlement = _service.Settle([
                                                                               Line(firstBeer, 350),
                                                                               Line(secondBeer, 100, "Der Tisch zahlt den Rest spaeter"),
                                                                               Line(hotdog, 500)
                                                                             ],
                                                                             _collectingWaiter,
                                                                             AtOneTable(firstBeer, secondBeer, hotdog),
                                                                             _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.True);
                      Assert.That(secondBeer.ChargedPriceCents, Is.EqualTo(100));
                      Assert.That(hotdog.ChargedPriceCents, Is.EqualTo(500));
                      Assert.That(secondBeer.ChargedPriceCents + hotdog.ChargedPriceCents, Is.EqualTo(600));
                      Assert.That(firstBeer.ChargedPriceCents, Is.Zero);
                      Assert.That(firstBeer.PaymentNotice, Is.EqualTo("Kapelle"));
                      Assert.That(firstBeer.SettledByStaffMemberId, Is.EqualTo(_anotherWaiter));
                    });
  }

  private SettleLineRequest Line(OrderItem item, int? paidPriceCents, string? paymentNotice = null)
  {
    return new()
           {
             OrderItemId = item.Id,
             PaidPriceCents = paidPriceCents,
             PaymentNotice = paymentNotice
           };
  }

  private IReadOnlyCollection<OrderItem> AtOneTable(params OrderItem[] items)
  {
    foreach (var item in items)
      PutAtTable("Tisch 3", item);

    return items.ToList();
  }

  private void PutAtTable(string tableName, OrderItem item)
  {
    Order order = new()
                  {
                    Id = Guid.NewGuid(),
                    ClientOrderId = Guid.NewGuid(),
                    FestivalId = Guid.NewGuid(),
                    GlobalOrderNumber = 1,
                    StaffMemberId = Guid.NewGuid(),
                    TableName = tableName,
                    CreatedAtUtc = _earlier
                  };

    StationOrder stationOrder = new()
                                {
                                  Id = item.StationOrderId,
                                  OrderId = order.Id,
                                  FestivalId = order.FestivalId,
                                  StationId = Guid.NewGuid(),
                                  StationOrderNumber = 1,
                                  DeliveryMode = DeliveryMode.Together,
                                  Order = order
                                };

    stationOrder.Items.Add(item);
    order.StationOrders.Add(stationOrder);
    item.StationOrder = stationOrder;
  }

  private OrderItem OpenItem(int unitPriceCents)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             StationOrderId = Guid.NewGuid(),
             CatalogItemId = Guid.NewGuid(),
             ItemName = "Artikel",
             UnitPriceCents = unitPriceCents
           };
  }
}
