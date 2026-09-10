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
  public void Settle_TheFullAmount_ChargesEveryItemItsOwnPrice()
  {
    var bratwurst = OpenItem(350);
    var beer = OpenItem(400);

    var settlement = _service.Settle(RequestFor([bratwurst.Id, beer.Id], 750), AtOneTable(bratwurst, beer), _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.True);
                      Assert.That(bratwurst.ChargedPriceCents, Is.EqualTo(350));
                      Assert.That(beer.ChargedPriceCents, Is.EqualTo(400));
                      Assert.That(bratwurst.SettledAtUtc, Is.EqualTo(_now));
                      Assert.That(beer.SettledAtUtc, Is.EqualTo(_now));
                      Assert.That(bratwurst.PaymentNotice, Is.Null);
                      Assert.That(beer.PaymentNotice, Is.Null);
                    });
  }

  [Test]
  public void Settle_NothingAtAllWithAReason_ChargesEveryItemZeroAndKeepsTheDisplayedPrice()
  {
    var bratwurst = OpenItem(350);
    var beer = OpenItem(400);

    var settlement = _service.Settle(RequestFor([bratwurst.Id, beer.Id], 0, "  Essen fuer die Kapelle  "),
                                     AtOneTable(bratwurst, beer),
                                     _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.True);
                      Assert.That(bratwurst.ChargedPriceCents, Is.Zero);
                      Assert.That(beer.ChargedPriceCents, Is.Zero);
                      Assert.That(bratwurst.UnitPriceCents, Is.EqualTo(350));
                      Assert.That(beer.UnitPriceCents, Is.EqualTo(400));
                      Assert.That(bratwurst.PaymentNotice, Is.EqualTo("Essen fuer die Kapelle"));
                      Assert.That(beer.PaymentNotice, Is.EqualTo("Essen fuer die Kapelle"));
                    });
  }

  [Test]
  public void Settle_APartOfTheAmount_SplitsItAcrossTheItemsByWhatEachOneCosts()
  {
    var cola = OpenItem(100);
    var beer = OpenItem(200);
    var bratwurst = OpenItem(300);

    var settlement = _service.Settle(RequestFor([cola.Id, beer.Id, bratwurst.Id],
                                                300,
                                                "Der Tisch zahlt den Rest spaeter"),
                                     AtOneTable(cola, beer, bratwurst),
                                     _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.True);
                      Assert.That(cola.ChargedPriceCents, Is.EqualTo(50));
                      Assert.That(beer.ChargedPriceCents, Is.EqualTo(100));
                      Assert.That(bratwurst.ChargedPriceCents, Is.EqualTo(150));
                    });
  }

  [Test]
  public void Settle_AnAmountThatDoesNotDivideEvenly_HandsOutEveryRemainingCentInTheOrderTheItemsWereNamed()
  {
    var first = OpenItem(333);
    var second = OpenItem(333);
    var third = OpenItem(333);

    var settlement = _service.Settle(RequestFor([first.Id, second.Id, third.Id],
                                                500,
                                                "Der Tisch zahlt den Rest spaeter"),
                                     AtOneTable(first, second, third),
                                     _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.True);
                      Assert.That(first.ChargedPriceCents, Is.EqualTo(167));
                      Assert.That(second.ChargedPriceCents, Is.EqualTo(167));
                      Assert.That(third.ChargedPriceCents, Is.EqualTo(166));
                      Assert.That(first.ChargedPriceCents + second.ChargedPriceCents + third.ChargedPriceCents,
                                  Is.EqualTo(500));
                    });
  }

  [Test]
  public void Settle_ASelectionWhereEveryItemIsPricedAtZero_SplitsTheAmountEquallyAndHandsOutTheRemainder()
  {
    var first = OpenItem(0);
    var second = OpenItem(0);
    var third = OpenItem(0);

    var settlement = _service.Settle(RequestFor([first.Id, second.Id, third.Id], 100),
                                     AtOneTable(first, second, third),
                                     _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.True);
                      Assert.That(first.ChargedPriceCents, Is.EqualTo(34));
                      Assert.That(second.ChargedPriceCents, Is.EqualTo(33));
                      Assert.That(third.ChargedPriceCents, Is.EqualTo(33));
                      Assert.That(first.ChargedPriceCents + second.ChargedPriceCents + third.ChargedPriceCents,
                                  Is.EqualTo(100));
                    });
  }

  [Test]
  public void Settle_MoreThanTheItemsCost_ChargesTheWholeAmountAndNeedsNoReason()
  {
    var bratwurst = OpenItem(350);
    var beer = OpenItem(350);

    var settlement = _service.Settle(RequestFor([bratwurst.Id, beer.Id], 1000), AtOneTable(bratwurst, beer), _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.True);
                      Assert.That(bratwurst.ChargedPriceCents, Is.EqualTo(500));
                      Assert.That(beer.ChargedPriceCents, Is.EqualTo(500));
                      Assert.That(bratwurst.PaymentNotice, Is.Null);
                    });
  }

  [Test]
  public void Settle_ANegativeAmount_IsRefusedAndNothingIsSettled()
  {
    var bratwurst = OpenItem(350);

    var settlement = _service.Settle(RequestFor([bratwurst.Id], -1, "Ein Grund"), AtOneTable(bratwurst), _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.False);
                      Assert.That(settlement.Failure.Reason, Is.EqualTo(SettlementFailureReason.AmountPaidNegative));
                      Assert.That(bratwurst.SettledAtUtc, Is.Null);
                      Assert.That(bratwurst.ChargedPriceCents, Is.Null);
                    });
  }

  [Test]
  public void Settle_LessThanTheItemsCostWithoutAReason_IsRefusedAndNothingIsSettled()
  {
    var bratwurst = OpenItem(350);
    var beer = OpenItem(400);

    var settlement = _service.Settle(RequestFor([bratwurst.Id, beer.Id], 700, "   "), AtOneTable(bratwurst, beer), _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.False);
                      Assert.That(settlement.Failure.Reason, Is.EqualTo(SettlementFailureReason.PaymentNoticeMissing));
                      Assert.That(bratwurst.SettledAtUtc, Is.Null);
                      Assert.That(beer.SettledAtUtc, Is.Null);
                    });
  }

  [Test]
  public void Settle_LessThanTheItemsCostWithAReason_SettlesTheItemsAndKeepsTheReason()
  {
    var bratwurst = OpenItem(350);
    var beer = OpenItem(400);

    var settlement = _service.Settle(RequestFor([bratwurst.Id, beer.Id], 700, "Der Tisch zahlt den Rest spaeter"),
                                     AtOneTable(bratwurst, beer),
                                     _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.True);
                      Assert.That(bratwurst.ChargedPriceCents + beer.ChargedPriceCents, Is.EqualTo(700));
                      Assert.That(bratwurst.PaymentNotice, Is.EqualTo("Der Tisch zahlt den Rest spaeter"));
                      Assert.That(beer.PaymentNotice, Is.EqualTo("Der Tisch zahlt den Rest spaeter"));
                    });
  }

  [Test]
  public void Settle_AnAmountThatCoversTheStillOpenItemsBesideASettledOne_NeedsNoReason()
  {
    var alreadyPaid = OpenItem(400);
    _service.MarkSettled(alreadyPaid, 400, null, _anotherWaiter, _earlier);
    var bratwurst = OpenItem(350);

    var settlement = _service.Settle(RequestFor([alreadyPaid.Id, bratwurst.Id], 350),
                                     AtOneTable(alreadyPaid, bratwurst),
                                     _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.True);
                      Assert.That(bratwurst.ChargedPriceCents, Is.EqualTo(350));
                    });
  }

  [Test]
  public void Settle_AnItemSomebodyElseAlreadySettled_GivesItNoShareAndLeavesTheFirstSettlementAsItWas()
  {
    var beer = OpenItem(400);
    _service.MarkSettled(beer, 0, "Kapelle", _anotherWaiter, _earlier);
    var bratwurst = OpenItem(350);

    var settlement = _service.Settle(RequestFor([beer.Id, bratwurst.Id], 350), AtOneTable(beer, bratwurst), _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.True);
                      Assert.That(settlement.Value.NewlySettled, Has.Count.EqualTo(1));
                      Assert.That(settlement.Value.AlreadySettledBeforehand, Has.Count.EqualTo(1));
                      Assert.That(beer.SettledAtUtc, Is.EqualTo(_earlier));
                      Assert.That(beer.ChargedPriceCents, Is.Zero);
                      Assert.That(beer.PaymentNotice, Is.EqualTo("Kapelle"));
                      Assert.That(bratwurst.ChargedPriceCents, Is.EqualTo(350));
                    });
  }

  [Test]
  public void Settle_TheItemsOfATable_RecordsTheWaiterWhoCollectedTheMoneyBesideTheTimeAndTheAmount()
  {
    var bratwurst = OpenItem(350);

    _service.Settle(RequestFor([bratwurst.Id], 350), AtOneTable(bratwurst), _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(bratwurst.SettledAtUtc, Is.EqualTo(_now));
                      Assert.That(bratwurst.ChargedPriceCents, Is.EqualTo(350));
                      Assert.That(bratwurst.SettledByStaffMemberId, Is.EqualTo(_collectingWaiter));
                    });
  }

  [Test]
  public void Settle_ItemsNobodySelected_LeavesTheirTimeAmountAndWaiterAllUnwritten()
  {
    var bratwurst = OpenItem(350);
    var beer = OpenItem(400);

    _service.Settle(RequestFor([bratwurst.Id], 350), AtOneTable(bratwurst, beer), _now);

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
                                         OrderItemIds = [bratwurst.Id],
                                         AmountPaidCents = 350,
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
  public void Settle_NothingSelected_IsRefused()
  {
    var settlement = _service.Settle(RequestFor([], 0), AtOneTable(), _now);

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

    var settlement = _service.Settle(RequestFor([bratwurst.Id, unknownId], 350), AtOneTable(bratwurst), _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.False);
                      Assert.That(settlement.Failure.Reason, Is.EqualTo(SettlementFailureReason.UnknownOrderItemId));
                      Assert.That(settlement.Failure.OffendingOrderItemId, Is.EqualTo(unknownId));
                      Assert.That(bratwurst.SettledAtUtc, Is.Null);
                    });
  }

  [Test]
  public void Settle_ASelectionSpanningTwoTables_IsRefusedAndNothingIsSettled()
  {
    var bratwurst = OpenItem(350);
    var beer = OpenItem(400);
    IReadOnlyCollection<SettlementCandidate> twoTables =
      [.. At("Tisch 12", bratwurst), .. At("Tisch 3", beer)];

    var settlement = _service.Settle(RequestFor([bratwurst.Id, beer.Id], 750), twoTables, _now);

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
  public void Settle_AnItemOfAnotherTableThatSomebodyElseAlreadySettled_SettlesTheTableThatIsStillOpen()
  {
    var beer = OpenItem(400);
    _service.MarkSettled(beer, 400, null, _anotherWaiter, _earlier);
    var bratwurst = OpenItem(350);
    IReadOnlyCollection<SettlementCandidate> twoTables =
      [.. At("Tisch 3", beer), .. At("Tisch 12", bratwurst)];

    var settlement = _service.Settle(RequestFor([beer.Id, bratwurst.Id], 350), twoTables, _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.True);
                      Assert.That(bratwurst.ChargedPriceCents, Is.EqualTo(350));
                      Assert.That(beer.SettledAtUtc, Is.EqualTo(_earlier));
                    });
  }

  [Test]
  public void Settle_TheSameItemNamedTwiceInOneSelection_SettlesItOnce()
  {
    var bratwurst = OpenItem(350);

    var settlement = _service.Settle(RequestFor([bratwurst.Id, bratwurst.Id], 350), AtOneTable(bratwurst), _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.True);
                      Assert.That(settlement.Value.NewlySettled, Has.Count.EqualTo(1));
                      Assert.That(bratwurst.ChargedPriceCents, Is.EqualTo(350));
                    });
  }

  [Test]
  public void Settle_MoreRepeatedIdsThanTheLimitButFewDistinctOnes_SettlesWhatWasActuallySelected()
  {
    var bratwurst = OpenItem(350);
    List<Guid> namedSixHundredTimes = [.. Enumerable.Repeat(bratwurst.Id, 600)];

    var settlement = _service.Settle(RequestFor(namedSixHundredTimes, 350), AtOneTable(bratwurst), _now);

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

    var settlement = _service.Settle(RequestFor(tooMany, 0), AtOneTable(), _now);

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
    _service.Settle(RequestFor([beer.Id], 0, "Kapelle"), AtOneTable(beer), _earlier);

    Assert.That(_service.WaivedAmountCentsOf(beer), Is.EqualTo(400));
  }

  [Test]
  public void WaivedAmountCentsOf_AnItemStillOpen_CountsNothingBecauseNothingWasGivenAwayYet()
  {
    Assert.That(_service.WaivedAmountCentsOf(OpenItem(400)), Is.Zero);
  }

  [Test]
  public void WaivedAmountCentsOf_AnItemTheTableOnlyPaidAPartOf_CountsWhatIsStillMissing()
  {
    var beer = OpenItem(400);
    _service.Settle(RequestFor([beer.Id], 150, "Der Tisch zahlt den Rest spaeter"), AtOneTable(beer), _earlier);

    Assert.That(_service.WaivedAmountCentsOf(beer), Is.EqualTo(250));
  }

  [Test]
  public void OpenAmountCentsOf_AMixOfOpenAndSettledItems_CountsOnlyWhatIsStillOpen()
  {
    var bratwurst = OpenItem(350);
    var beer = OpenItem(400);
    _service.MarkSettled(beer, 400, null, _anotherWaiter, _earlier);

    Assert.That(_service.OpenAmountCentsOf([bratwurst, beer]), Is.EqualTo(350));
  }

  [Test]
  public void WaivedAmountCentsOf_AnItemGivenAwayBesideOneThatWasPaid_CountsOnlyTheGivenAwayPrice()
  {
    var beer = OpenItem(400);
    var bratwurst = OpenItem(350);
    _service.MarkSettled(beer, 0, "Kapelle", _anotherWaiter, _earlier);
    _service.MarkSettled(bratwurst, 350, null, _anotherWaiter, _earlier);

    Assert.That(_service.WaivedAmountCentsOf([beer, bratwurst]), Is.EqualTo(400));
  }

  private SettlementRequest RequestFor(IReadOnlyList<Guid> orderItemIds,
                                       int amountPaidCents,
                                       string? paymentNotice = null)
  {
    return new()
           {
             OrderItemIds = orderItemIds,
             AmountPaidCents = amountPaidCents,
             SettledByStaffMemberId = _collectingWaiter,
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
