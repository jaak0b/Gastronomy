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
  public void Settle_OnePricePerLine_StoresExactlyThePricesThePhoneSent()
  {
    var bratwurst = OpenItem(350);
    var beer = OpenItem(400);

    var settlement = _service.Settle(RequestFor([Line(bratwurst, 200, "Stammgast"), Line(beer, 150, "Stammgast")]),
                                     AtOneTable(bratwurst, beer),
                                     _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.True);
                      Assert.That(settlement.Value.NewlySettled, Has.Count.EqualTo(2));
                      Assert.That(bratwurst.ChargedPriceCents, Is.EqualTo(200));
                      Assert.That(beer.ChargedPriceCents, Is.EqualTo(150));
                      Assert.That(bratwurst.PaymentNotice, Is.EqualTo("Stammgast"));
                      Assert.That(beer.PaymentNotice, Is.EqualTo("Stammgast"));
                      Assert.That(bratwurst.SettledAtUtc, Is.EqualTo(_now));
                      Assert.That(beer.SettledAtUtc, Is.EqualTo(_now));
                      Assert.That(bratwurst.SettledByStaffMemberId, Is.EqualTo(_collectingWaiter));
                      Assert.That(beer.SettledByStaffMemberId, Is.EqualTo(_collectingWaiter));
                    });
  }

  [Test]
  public void Settle_MoreThanTheItemCosts_StoresTheSentPriceAsItIs()
  {
    var bratwurst = OpenItem(350);

    var settlement = _service.Settle(RequestFor([Line(bratwurst, 500)]), AtOneTable(bratwurst), _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.True);
                      Assert.That(bratwurst.ChargedPriceCents, Is.EqualTo(500));
                      Assert.That(bratwurst.PaymentNotice, Is.Null);
                    });
  }

  [Test]
  public void Settle_AShortLineWithAReason_KeepsTheTrimmedReason()
  {
    var bratwurst = OpenItem(350);

    var settlement = _service.Settle(RequestFor([Line(bratwurst, 150, "  Der Tisch zahlt den Rest spaeter  ")]),
                                     AtOneTable(bratwurst),
                                     _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.True);
                      Assert.That(bratwurst.ChargedPriceCents, Is.EqualTo(150));
                      Assert.That(bratwurst.PaymentNotice, Is.EqualTo("Der Tisch zahlt den Rest spaeter"));
                    });
  }

  [Test]
  public void Settle_ALineTheCallerAlreadySettled_OverwritesThePriceAndTheReasonAndKeepsTimeAndCollector()
  {
    var beer = OpenItem(400);
    _service.MarkSettled(beer, 0, "Kapelle", _collectingWaiter, _earlier);

    var settlement = _service.Settle(RequestFor([Line(beer, 400)]), AtOneTable(beer), _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.True);
                      Assert.That(settlement.Value.NewlySettled, Is.Empty);
                      Assert.That(settlement.Value.Reapplied, Has.Count.EqualTo(1));
                      Assert.That(settlement.Value.AlreadySettledByOthers, Is.Empty);
                      Assert.That(beer.ChargedPriceCents, Is.EqualTo(400));
                      Assert.That(beer.PaymentNotice, Is.Null);
                      Assert.That(beer.SettledAtUtc, Is.EqualTo(_earlier));
                      Assert.That(beer.SettledByStaffMemberId, Is.EqualTo(_collectingWaiter));
                    });
  }

  [Test]
  public void Settle_ALineSomebodyElseAlreadySettled_IsNeverChangedAndIsReportedBack()
  {
    var beer = OpenItem(400);
    _service.MarkSettled(beer, 0, "Kapelle", _anotherWaiter, _earlier);

    var settlement = _service.Settle(RequestFor([Line(beer, 400)]), AtOneTable(beer), _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.True);
                      Assert.That(settlement.Value.NewlySettled, Is.Empty);
                      Assert.That(settlement.Value.Reapplied, Is.Empty);
                      Assert.That(settlement.Value.AlreadySettledByOthers, Has.Count.EqualTo(1));
                      Assert.That(beer.ChargedPriceCents, Is.Zero);
                      Assert.That(beer.PaymentNotice, Is.EqualTo("Kapelle"));
                      Assert.That(beer.SettledAtUtc, Is.EqualTo(_earlier));
                      Assert.That(beer.SettledByStaffMemberId, Is.EqualTo(_anotherWaiter));
                    });
  }

  [Test]
  public void Settle_AnOpenLineBesideALineSomebodyElseAlreadySettled_SettlesOnlyTheOpenLine()
  {
    var beer = OpenItem(400);
    _service.MarkSettled(beer, 400, null, _anotherWaiter, _earlier);
    var bratwurst = OpenItem(350);

    var settlement = _service.Settle(RequestFor([Line(beer, 400), Line(bratwurst, 350)]),
                                     AtOneTable(beer, bratwurst),
                                     _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.True);
                      Assert.That(settlement.Value.NewlySettled, Has.Count.EqualTo(1));
                      Assert.That(settlement.Value.NewlySettled[0], Is.SameAs(bratwurst));
                      Assert.That(settlement.Value.AlreadySettledByOthers, Has.Count.EqualTo(1));
                      Assert.That(settlement.Value.AlreadySettledByOthers[0], Is.SameAs(beer));
                      Assert.That(bratwurst.ChargedPriceCents, Is.EqualTo(350));
                      Assert.That(beer.ChargedPriceCents, Is.EqualTo(400));
                    });
  }

  [Test]
  public void Settle_ALineWithoutAPrice_IsRefusedAndNothingIsSettled()
  {
    var bratwurst = OpenItem(350);

    var settlement = _service.Settle(RequestFor([Line(bratwurst, null)]), AtOneTable(bratwurst), _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.False);
                      Assert.That(settlement.Failure.Reason, Is.EqualTo(SettlementFailureReason.AmountPaidMissing));
                      Assert.That(settlement.Failure.OffendingOrderItemId, Is.EqualTo(bratwurst.Id));
                      Assert.That(bratwurst.SettledAtUtc, Is.Null);
                      Assert.That(bratwurst.ChargedPriceCents, Is.Null);
                    });
  }

  [Test]
  public void Settle_ANegativePrice_IsRefusedAndNothingIsSettled()
  {
    var bratwurst = OpenItem(350);

    var settlement = _service.Settle(RequestFor([Line(bratwurst, -1, "Ein Grund")]), AtOneTable(bratwurst), _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.False);
                      Assert.That(settlement.Failure.Reason, Is.EqualTo(SettlementFailureReason.AmountPaidNegative));
                      Assert.That(settlement.Failure.OffendingOrderItemId, Is.EqualTo(bratwurst.Id));
                      Assert.That(bratwurst.SettledAtUtc, Is.Null);
                      Assert.That(bratwurst.ChargedPriceCents, Is.Null);
                    });
  }

  [Test]
  public void Settle_AShortLineWithoutAReason_IsRefusedAndNothingIsSettled()
  {
    var bratwurst = OpenItem(350);

    var settlement = _service.Settle(RequestFor([Line(bratwurst, 300, "   ")]), AtOneTable(bratwurst), _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.False);
                      Assert.That(settlement.Failure.Reason, Is.EqualTo(SettlementFailureReason.PaymentNoticeMissing));
                      Assert.That(settlement.Failure.OffendingOrderItemId, Is.EqualTo(bratwurst.Id));
                      Assert.That(bratwurst.SettledAtUtc, Is.Null);
                    });
  }

  [Test]
  public void Settle_ALaterLineThatFailsValidation_LeavesTheEarlierLineUntouched()
  {
    var bratwurst = OpenItem(350);
    var beer = OpenItem(400);

    var settlement = _service.Settle(RequestFor([Line(bratwurst, 350), Line(beer, null)]),
                                     AtOneTable(bratwurst, beer),
                                     _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.False);
                      Assert.That(settlement.Failure.Reason, Is.EqualTo(SettlementFailureReason.AmountPaidMissing));
                      Assert.That(bratwurst.SettledAtUtc, Is.Null);
                      Assert.That(bratwurst.ChargedPriceCents, Is.Null);
                    });
  }

  [Test]
  public void Settle_ASelectionSpanningTwoTables_IsRefusedAndNothingIsSettled()
  {
    var bratwurst = OpenItem(350);
    var beer = OpenItem(400);
    IReadOnlyCollection<SettlementCandidate> twoTables =
      [.. At("Tisch 12", bratwurst), .. At("Tisch 3", beer)];

    var settlement = _service.Settle(RequestFor([Line(bratwurst, 350), Line(beer, 400)]), twoTables, _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.False);
                      Assert.That(settlement.Failure.Reason,
                                  Is.EqualTo(SettlementFailureReason.SelectionSpansSeveralTables));
                      Assert.That(settlement.Failure.TableNamesInTheSelection,
                                  Is.EqualTo(new[] { "Tisch 12", "Tisch 3" }));
                      Assert.That(bratwurst.SettledAtUtc, Is.Null);
                      Assert.That(beer.SettledAtUtc, Is.Null);
                    });
  }

  [Test]
  public void Settle_ASelectionSpanningTwoTablesWhereOneLineIsAlreadySettled_IsRefusedAsWell()
  {
    var bratwurst = OpenItem(350);
    var beer = OpenItem(400);
    _service.MarkSettled(beer, 400, null, _anotherWaiter, _earlier);
    IReadOnlyCollection<SettlementCandidate> twoTables =
      [.. At("Tisch 12", bratwurst), .. At("Tisch 3", beer)];

    var settlement = _service.Settle(RequestFor([Line(bratwurst, 350), Line(beer, 400)]), twoTables, _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.False);
                      Assert.That(settlement.Failure.Reason,
                                  Is.EqualTo(SettlementFailureReason.SelectionSpansSeveralTables));
                      Assert.That(bratwurst.SettledAtUtc, Is.Null);
                    });
  }

  [Test]
  public void Settle_TheSameLineNamedTwice_IsRefusedAndNothingIsSettled()
  {
    var bratwurst = OpenItem(350);

    var settlement = _service.Settle(RequestFor([Line(bratwurst, 350), Line(bratwurst, 350)]),
                                     AtOneTable(bratwurst),
                                     _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.False);
                      Assert.That(settlement.Failure.Reason, Is.EqualTo(SettlementFailureReason.DuplicateOrderItemId));
                      Assert.That(settlement.Failure.OffendingOrderItemId, Is.EqualTo(bratwurst.Id));
                      Assert.That(bratwurst.SettledAtUtc, Is.Null);
                      Assert.That(bratwurst.ChargedPriceCents, Is.Null);
                    });
  }

  [Test]
  public void Settle_ASelectionHoldingAnUnknownId_SettlesNoneOfTheSelection()
  {
    var bratwurst = OpenItem(350);
    var unknownId = Guid.NewGuid();

    var settlement = _service.Settle(RequestFor([Line(bratwurst, 350), Line(unknownId, 350)]),
                                     AtOneTable(bratwurst),
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
  public void Settle_NothingSelected_IsRefused()
  {
    var settlement = _service.Settle(RequestFor([]), AtOneTable(), _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.False);
                      Assert.That(settlement.Failure.Reason, Is.EqualTo(SettlementFailureReason.NoItemsSelected));
                    });
  }

  [Test]
  public void Settle_WithoutNamingTheWaiter_IsRefusedAndNothingIsSettled()
  {
    var bratwurst = OpenItem(350);
    SettlementRequest withoutAWaiter = new()
                                       {
                                         Lines = [Line(bratwurst, 350)],
                                         SettledByStaffMemberId = Guid.Empty
                                       };

    Assert.Multiple(() =>
                    {
                      Assert.That(() => _service.Settle(withoutAWaiter, AtOneTable(bratwurst), _now),
                                  Throws.ArgumentException);
                      Assert.That(bratwurst.SettledAtUtc, Is.Null);
                      Assert.That(bratwurst.ChargedPriceCents, Is.Null);
                      Assert.That(bratwurst.SettledByStaffMemberId, Is.Null);
                    });
  }

  [Test]
  public void Settle_ItemsNobodySelected_LeavesTheirTimeAmountAndWaiterAllUnwritten()
  {
    var bratwurst = OpenItem(350);
    var beer = OpenItem(400);

    _service.Settle(RequestFor([Line(bratwurst, 350)]), AtOneTable(bratwurst, beer), _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(beer.SettledAtUtc, Is.Null);
                      Assert.That(beer.ChargedPriceCents, Is.Null);
                      Assert.That(beer.SettledByStaffMemberId, Is.Null);
                    });
  }

  [Test]
  public void CalculateWaivedAmountCents_OneItemGivenAway_CountsTheDisplayedPriceOfThatItemAlone()
  {
    var beer = OpenItem(400);
    _service.Settle(RequestFor([Line(beer, 0, "Kapelle")]), AtOneTable(beer), _earlier);

    Assert.That(_service.CalculateWaivedAmountCents(beer), Is.EqualTo(400));
  }

  [Test]
  public void CalculateWaivedAmountCents_AnItemStillOpen_CountsNothingBecauseNothingWasGivenAwayYet()
  {
    Assert.That(_service.CalculateWaivedAmountCents(OpenItem(400)), Is.Zero);
  }

  [Test]
  public void CalculateWaivedAmountCents_AnItemTheTableOnlyPaidAPartOf_CountsWhatIsStillMissing()
  {
    var beer = OpenItem(400);
    _service.Settle(RequestFor([Line(beer, 150, "Der Tisch zahlt den Rest spaeter")]), AtOneTable(beer), _earlier);

    Assert.That(_service.CalculateWaivedAmountCents(beer), Is.EqualTo(250));
  }

  [Test]
  public void SumOpenAmountCents_AMixOfOpenAndSettledItems_CountsOnlyWhatIsStillOpen()
  {
    var bratwurst = OpenItem(350);
    var beer = OpenItem(400);
    _service.MarkSettled(beer, 400, null, _anotherWaiter, _earlier);

    Assert.That(_service.SumOpenAmountCents([bratwurst, beer]), Is.EqualTo(350));
  }

  [Test]
  public void SumWaivedAmountCents_AnItemGivenAwayBesideOneThatWasPaid_CountsOnlyTheGivenAwayPrice()
  {
    var beer = OpenItem(400);
    var bratwurst = OpenItem(350);
    _service.MarkSettled(beer, 0, "Kapelle", _anotherWaiter, _earlier);
    _service.MarkSettled(bratwurst, 350, null, _anotherWaiter, _earlier);

    Assert.That(_service.SumWaivedAmountCents([beer, bratwurst]), Is.EqualTo(400));
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

  private SettlementLine Line(Guid orderItemId, int? paidPriceCents, string? paymentNotice = null)
  {
    return new()
           {
             OrderItemId = orderItemId,
             PaidPriceCents = paidPriceCents,
             PaymentNotice = paymentNotice
           };
  }

  private IReadOnlyCollection<SettlementCandidate> AtOneTable(params OrderItem[] items)
  {
    return At("Tisch 12", items);
  }

  private IReadOnlyCollection<SettlementCandidate> At(string tableName, params OrderItem[] items)
  {
    return [.. items.Select(item => new SettlementCandidate { Item = item, TableName = tableName })];
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
