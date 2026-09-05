using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class OrderItemSettlementServiceTest
{
  [SetUp]
  public void SetUp()
  {
    _service = new();
  }

  private readonly DateTime _now = new(2026, 9, 5, 20, 15, 0, DateTimeKind.Utc);
  private readonly DateTime _earlier = new(2026, 9, 5, 19, 5, 0, DateTimeKind.Utc);
  private readonly Guid _collectingWaiter = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
  private readonly Guid _anotherWaiter = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

  private OrderItemSettlementService _service = null!;

  [Test]
  public void Settle_AtTheDisplayedPrice_ChargesTheDisplayedPriceAndLeavesItStanding()
  {
    var bratwurst = OpenItem(350);

    var settlement = _service.Settle(RequestFor(SettlementKind.AtTheDisplayedPrice, [bratwurst.Id]),
                                     [bratwurst],
                                     _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.True);
                      Assert.That(bratwurst.SettledAtUtc, Is.EqualTo(_now));
                      Assert.That(bratwurst.ChargedPriceCents, Is.EqualTo(350));
                      Assert.That(bratwurst.UnitPriceCents, Is.EqualTo(350));
                      Assert.That(bratwurst.PaymentNotice, Is.Null);
                    });
  }

  [Test]
  public void Settle_FreeOfCharge_ChargesNothingAndKeepsTheDisplayedPriceAndTheReason()
  {
    var beer = OpenItem(400);

    var settlement = _service.Settle(RequestFor(SettlementKind.FreeOfCharge, [beer.Id], "  Getraenk fuer die Kapelle  "),
                                     [beer],
                                     _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.True);
                      Assert.That(beer.SettledAtUtc, Is.EqualTo(_now));
                      Assert.That(beer.ChargedPriceCents, Is.Zero);
                      Assert.That(beer.UnitPriceCents, Is.EqualTo(400));
                      Assert.That(beer.PaymentNotice, Is.EqualTo("Getraenk fuer die Kapelle"));
                    });
  }

  [Test]
  public void Settle_FreeOfChargeWithoutAReason_IsRefusedAndNothingIsSettled()
  {
    var beer = OpenItem(400);

    var settlement = _service.Settle(RequestFor(SettlementKind.FreeOfCharge, [beer.Id], "   "), [beer], _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.False);
                      Assert.That(settlement.Failure.Reason, Is.EqualTo(SettlementFailureReason.PaymentNoticeMissing));
                      Assert.That(beer.SettledAtUtc, Is.Null);
                    });
  }

  [Test]
  public void Settle_AtTheDisplayedPrice_RecordsTheWaiterWhoCollectedTheMoneyBesideTheTimeAndTheAmount()
  {
    var bratwurst = OpenItem(350);

    _service.Settle(RequestFor(SettlementKind.AtTheDisplayedPrice, [bratwurst.Id]), [bratwurst], _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(bratwurst.SettledAtUtc, Is.EqualTo(_now));
                      Assert.That(bratwurst.ChargedPriceCents, Is.EqualTo(350));
                      Assert.That(bratwurst.SettledByStaffMemberId, Is.EqualTo(_collectingWaiter));
                    });
  }

  [Test]
  public void Settle_FreeOfCharge_RecordsTheWaiterWhoGaveTheItemAwayBesideTheTimeAndTheAmount()
  {
    var beer = OpenItem(400);

    _service.Settle(RequestFor(SettlementKind.FreeOfCharge, [beer.Id], "Kapelle"), [beer], _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(beer.SettledAtUtc, Is.EqualTo(_now));
                      Assert.That(beer.ChargedPriceCents, Is.Zero);
                      Assert.That(beer.SettledByStaffMemberId, Is.EqualTo(_collectingWaiter));
                    });
  }

  [Test]
  public void Settle_ItemsNobodySelected_LeavesTheirTimeAmountAndWaiterAllUnwritten()
  {
    var bratwurst = OpenItem(350);
    var beer = OpenItem(400);

    _service.Settle(RequestFor(SettlementKind.AtTheDisplayedPrice, [bratwurst.Id]), [bratwurst, beer], _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(beer.SettledAtUtc, Is.Null);
                      Assert.That(beer.ChargedPriceCents, Is.Null);
                      Assert.That(beer.SettledByStaffMemberId, Is.Null);
                    });
  }

  [Test]
  public void Settle_WithoutNamingTheWaiter_IsRefusedAndNothingIsSettled()
  {
    var bratwurst = OpenItem(350);
    SettlementRequest withoutAWaiter = new()
                                       {
                                         Kind = SettlementKind.AtTheDisplayedPrice,
                                         OrderItemIds = [bratwurst.Id],
                                         SettledByStaffMemberId = Guid.Empty
                                       };

    Assert.Multiple(() =>
                    {
                      Assert.That(() => _service.Settle(withoutAWaiter, [bratwurst], _now),
                                  Throws.ArgumentException);
                      Assert.That(bratwurst.SettledAtUtc, Is.Null);
                      Assert.That(bratwurst.ChargedPriceCents, Is.Null);
                      Assert.That(bratwurst.SettledByStaffMemberId, Is.Null);
                    });
  }

  [Test]
  public void Settle_NothingSelected_IsRefused()
  {
    var settlement = _service.Settle(RequestFor(SettlementKind.AtTheDisplayedPrice, []), [], _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.False);
                      Assert.That(settlement.Failure.Reason, Is.EqualTo(SettlementFailureReason.NoItemsSelected));
                    });
  }

  [Test]
  public void Settle_ASelectionHoldingAnUnknownId_SettlesNoneOfTheSelection()
  {
    var bratwurst = OpenItem(350);
    var unknownId = Guid.NewGuid();

    var settlement = _service.Settle(RequestFor(SettlementKind.AtTheDisplayedPrice, [bratwurst.Id, unknownId]),
                                     [bratwurst],
                                     _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.False);
                      Assert.That(settlement.Failure.Reason, Is.EqualTo(SettlementFailureReason.UnknownOrderItemId));
                      Assert.That(settlement.Failure.OffendingOrderItemId, Is.EqualTo(unknownId));
                      Assert.That(bratwurst.SettledAtUtc, Is.Null);
                    });
  }

  [Test]
  public void Settle_AnItemSomebodyElseAlreadySettled_LeavesTheFirstSettlementAsItWas()
  {
    var beer = OpenItem(400);
    _service.SettleFreeOfCharge(beer, "Kapelle", _anotherWaiter, _earlier);

    var settlement = _service.Settle(RequestFor(SettlementKind.AtTheDisplayedPrice, [beer.Id]), [beer], _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.True);
                      Assert.That(settlement.Value.NewlySettled, Is.Empty);
                      Assert.That(settlement.Value.AlreadySettledBeforehand, Has.Count.EqualTo(1));
                      Assert.That(beer.SettledAtUtc, Is.EqualTo(_earlier));
                      Assert.That(beer.ChargedPriceCents, Is.Zero);
                      Assert.That(beer.PaymentNotice, Is.EqualTo("Kapelle"));
                    });
  }

  [Test]
  public void Settle_TheSameItemNamedTwiceInOneSelection_SettlesItOnce()
  {
    var bratwurst = OpenItem(350);

    var settlement = _service.Settle(RequestFor(SettlementKind.AtTheDisplayedPrice, [bratwurst.Id, bratwurst.Id]),
                                     [bratwurst],
                                     _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.True);
                      Assert.That(settlement.Value.NewlySettled, Has.Count.EqualTo(1));
                    });
  }

  [Test]
  public void Settle_MoreRepeatedIdsThanTheLimitButFewDistinctOnes_SettlesWhatWasActuallySelected()
  {
    var bratwurst = OpenItem(350);
    List<Guid> namedSixHundredTimes = [.. Enumerable.Repeat(bratwurst.Id, 600)];

    var settlement = _service.Settle(RequestFor(SettlementKind.AtTheDisplayedPrice, namedSixHundredTimes),
                                     [bratwurst],
                                     _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.True);
                      Assert.That(settlement.Value.NewlySettled, Has.Count.EqualTo(1));
                    });
  }

  [Test]
  public void Settle_MoreDistinctIdsThanTheLimit_IsRefused()
  {
    List<Guid> tooMany = [.. Enumerable.Range(0, 501).Select(_ => Guid.NewGuid())];

    var settlement = _service.Settle(RequestFor(SettlementKind.AtTheDisplayedPrice, tooMany), [], _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.False);
                      Assert.That(settlement.Failure.Reason,
                                  Is.EqualTo(SettlementFailureReason.TooManyItemsSelected));
                    });
  }

  [Test]
  public void WaivedAmountCentsOf_OneItemGivenAway_CountsTheDisplayedPriceOfThatItemAlone()
  {
    var beer = OpenItem(400);
    _service.SettleFreeOfCharge(beer, "Kapelle", _anotherWaiter, _earlier);

    Assert.That(_service.WaivedAmountCentsOf(beer), Is.EqualTo(400));
  }

  [Test]
  public void WaivedAmountCentsOf_AnItemStillOpen_CountsNothingBecauseNothingWasGivenAwayYet()
  {
    Assert.That(_service.WaivedAmountCentsOf(OpenItem(400)), Is.Zero);
  }

  [Test]
  public void OpenAmountCentsOf_AMixOfOpenAndSettledItems_CountsOnlyWhatIsStillOpen()
  {
    var bratwurst = OpenItem(350);
    var beer = OpenItem(400);
    _service.SettleAtTheDisplayedPrice(beer, _anotherWaiter, _earlier);

    Assert.That(_service.OpenAmountCentsOf([bratwurst, beer]), Is.EqualTo(350));
  }

  [Test]
  public void WaivedAmountCentsOf_AnItemGivenAway_CountsTheDisplayedPrice()
  {
    var beer = OpenItem(400);
    var bratwurst = OpenItem(350);
    _service.SettleFreeOfCharge(beer, "Kapelle", _anotherWaiter, _earlier);
    _service.SettleAtTheDisplayedPrice(bratwurst, _anotherWaiter, _earlier);

    Assert.That(_service.WaivedAmountCentsOf([beer, bratwurst]), Is.EqualTo(400));
  }

  private SettlementRequest RequestFor(SettlementKind kind,
                                       IReadOnlyList<Guid> orderItemIds,
                                       string? paymentNotice = null)
  {
    return new()
           {
             Kind = kind,
             OrderItemIds = orderItemIds,
             SettledByStaffMemberId = _collectingWaiter,
             PaymentNotice = paymentNotice
           };
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
