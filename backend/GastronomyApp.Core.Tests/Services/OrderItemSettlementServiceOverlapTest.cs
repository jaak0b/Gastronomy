using FakeItEasy;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class OrderItemSettlementServiceOverlapTest
{
  [SetUp]
  public void SetUp()
  {
    _service = new(A.Fake<IOpenItemRepository>(), new(A.Fake<IFestivalRepository>(), new(), A.Fake<IClock>()), A.Fake<ITransactionRunner>(), A.Fake<IClock>());
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

    Result<SettlementResult, SettlementFailure> settlement = _service.Settle(RequestFor([
                                                                                          Line(firstBeer, 350),
                                                                                          Line(secondBeer, 350),
                                                                                          Line(hotdog, 400)
                                                                                        ]),
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

    Result<SettlementResult, SettlementFailure> settlement = _service.Settle(RequestFor([
                                                                                          Line(firstBeer, 350),
                                                                                          Line(secondBeer, 100, "Der Tisch zahlt den Rest spaeter"),
                                                                                          Line(hotdog, 500)
                                                                                        ]),
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

  private SettlementRequest RequestFor(IReadOnlyList<SettlementLine> lines)
  {
    return new()
           {
             Lines = lines,
             SettledByStaffMemberId = _collectingWaiter
           };
  }

  private SettlementLine Line(OrderItem item, int? paidPriceCents, string? paymentNotice = null)
  {
    return new()
           {
             OrderItemId = item.Id,
             PaidPriceCents = paidPriceCents,
             PaymentNotice = paymentNotice
           };
  }

  private IReadOnlyCollection<SettlementCandidate> AtOneTable(params OrderItem[] items)
  {
    return items.Select(item => new SettlementCandidate
                                {
                                  Item = item,
                                  TableName = "Tisch 3"
                                })
                .ToList();
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
