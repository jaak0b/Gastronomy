using FakeItEasy;
using GastronomyApp.Contracts;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using GastronomyApp.Core.Tests.TestSupport;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class OrderItemSettlementServiceTest
{
  [SetUp]
  public void SetUp()
  {
    _repository = A.Fake<IOpenItemRepository>();
    _festivalRepository = A.Fake<IFestivalRepository>();
    _clock = A.Fake<IClock>();
    _transactionRunner = new();

    A.CallTo(() => _clock.UtcNow).Returns(_now);
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(RunningFestival()));
    A.CallTo(() => _repository.FindForSettlementAsync(A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<OrderItem>>([]));

    _service = new(_repository, new(_festivalRepository, new(), _clock), _transactionRunner, _clock);
  }

  private readonly DateTime _now = new(2026, 9, 5, 20, 15, 0, DateTimeKind.Utc);
  private readonly DateTime _earlier = new(2026, 9, 5, 19, 5, 0, DateTimeKind.Utc);
  private readonly Guid _collectingWaiter = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
  private readonly Guid _anotherWaiter = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

  private IClock _clock = null!;
  private IFestivalRepository _festivalRepository = null!;
  private IOpenItemRepository _repository = null!;
  private OrderItemSettlementService _service = null!;
  private RecordingTransactionRunner _transactionRunner = null!;

  [Test]
  public void Settle_OnePricePerLine_StoresExactlyThePricesThePhoneSent()
  {
    var bratwurst = OpenItem(350);
    var beer = OpenItem(400);

    Result<SettlementResult, SettlementFailure> settlement = _service.Settle([
                                                                               Line(bratwurst, 200, "Stammgast"),
                                                                               Line(beer, 150, "Stammgast")
                                                                             ],
                                                                             _collectingWaiter,
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

    Result<SettlementResult, SettlementFailure> settlement = _service.Settle([Line(bratwurst, 500)], _collectingWaiter, AtOneTable(bratwurst), _now);

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

    Result<SettlementResult, SettlementFailure> settlement = _service.Settle([Line(bratwurst, 150, "  Der Tisch zahlt den Rest spaeter  ")], _collectingWaiter, AtOneTable(bratwurst), _now);

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

    Result<SettlementResult, SettlementFailure> settlement = _service.Settle([Line(beer, 400)], _collectingWaiter, AtOneTable(beer), _now);

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

    Result<SettlementResult, SettlementFailure> settlement = _service.Settle([Line(beer, 400)], _collectingWaiter, AtOneTable(beer), _now);

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

    Result<SettlementResult, SettlementFailure> settlement = _service.Settle([
                                                                               Line(beer, 400),
                                                                               Line(bratwurst, 350)
                                                                             ],
                                                                             _collectingWaiter,
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

    Result<SettlementResult, SettlementFailure> settlement = _service.Settle([Line(bratwurst, null)], _collectingWaiter, AtOneTable(bratwurst), _now);

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

    Result<SettlementResult, SettlementFailure> settlement = _service.Settle([Line(bratwurst, -1, "Ein Grund")], _collectingWaiter, AtOneTable(bratwurst), _now);

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

    Result<SettlementResult, SettlementFailure> settlement = _service.Settle([Line(bratwurst, 300, "   ")], _collectingWaiter, AtOneTable(bratwurst), _now);

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

    Result<SettlementResult, SettlementFailure> settlement = _service.Settle([
                                                                               Line(bratwurst, 350),
                                                                               Line(beer, null)
                                                                             ],
                                                                             _collectingWaiter,
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
    IReadOnlyCollection<OrderItem> twoTables = At("Tisch 12", bratwurst).Concat(At("Tisch 3", beer)).ToList();

    Result<SettlementResult, SettlementFailure> settlement = _service.Settle([
                                                                               Line(bratwurst, 350),
                                                                               Line(beer, 400)
                                                                             ],
                                                                             _collectingWaiter,
                                                                             twoTables,
                                                                             _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.False);
                      Assert.That(settlement.Failure.Reason, Is.EqualTo(SettlementFailureReason.SelectionSpansSeveralTables));
                      Assert.That(settlement.Failure.TableNamesInTheSelection,
                                  Is.EqualTo(new[]
                                             {
                                               "Tisch 12",
                                               "Tisch 3"
                                             }));
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
    IReadOnlyCollection<OrderItem> twoTables = At("Tisch 12", bratwurst).Concat(At("Tisch 3", beer)).ToList();

    Result<SettlementResult, SettlementFailure> settlement = _service.Settle([
                                                                               Line(bratwurst, 350),
                                                                               Line(beer, 400)
                                                                             ],
                                                                             _collectingWaiter,
                                                                             twoTables,
                                                                             _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.False);
                      Assert.That(settlement.Failure.Reason, Is.EqualTo(SettlementFailureReason.SelectionSpansSeveralTables));
                      Assert.That(bratwurst.SettledAtUtc, Is.Null);
                    });
  }

  [Test]
  public void Settle_TheSameLineNamedTwice_IsRefusedAndNothingIsSettled()
  {
    var bratwurst = OpenItem(350);

    Result<SettlementResult, SettlementFailure> settlement = _service.Settle([
                                                                               Line(bratwurst, 350),
                                                                               Line(bratwurst, 350)
                                                                             ],
                                                                             _collectingWaiter,
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

    Result<SettlementResult, SettlementFailure> settlement = _service.Settle([
                                                                               Line(bratwurst, 350),
                                                                               Line(unknownId, 350)
                                                                             ],
                                                                             _collectingWaiter,
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
    Result<SettlementResult, SettlementFailure> settlement = _service.Settle([], _collectingWaiter, AtOneTable(), _now);

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

    Assert.Multiple(() =>
                    {
                      Assert.That(() => _service.Settle([Line(bratwurst, 350)], Guid.Empty, AtOneTable(bratwurst), _now), Throws.ArgumentException);
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

    _service.Settle([Line(bratwurst, 350)], _collectingWaiter, AtOneTable(bratwurst, beer), _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(beer.SettledAtUtc, Is.Null);
                      Assert.That(beer.ChargedPriceCents, Is.Null);
                      Assert.That(beer.SettledByStaffMemberId, Is.Null);
                    });
  }

  [Test]
  public void SumOpenAmountCents_AMixOfOpenAndSettledItems_CountsOnlyWhatIsStillOpen()
  {
    var bratwurst = OpenItem(350);
    var beer = OpenItem(400);
    _service.MarkSettled(beer, 400, null, _anotherWaiter, _earlier);

    Assert.That(_service.SumOpenAmountCents([
                                              bratwurst,
                                              beer
                                            ]),
                Is.EqualTo(350));
  }

  [Test]
  public void SettleAsync_NullRequest_ThrowsArgumentNullException()
  {
    Assert.That(async () => await _service.SettleAsync(null!, _collectingWaiter, CancellationToken.None), Throws.ArgumentNullException);
  }

  [Test]
  public async Task SettleAsync_NoFestivalIsRunning_RefusesTheSettlementAndSavesNothing()
  {
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(null));

    Result<SettlementResult, SettlementFailure> settlement = await _service.SettleAsync([Line(Guid.NewGuid(), 350)], _collectingWaiter, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.False);
                      Assert.That(settlement.Failure.Reason, Is.EqualTo(SettlementFailureReason.NoRunningFestival));
                      Assert.That(_transactionRunner.Committed, Is.Null);
                    });

    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task SettleAsync_TheItemsTheScreenSent_SettlesThemAndCommitsTheTransaction()
  {
    var bratwurst = OpenItem(350);
    GivenTheTableHolds("Tisch 12", bratwurst);

    Result<SettlementResult, SettlementFailure> settlement = await _service.SettleAsync([Line(bratwurst, 350)], _collectingWaiter, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.True);
                      Assert.That(settlement.Value.NewlySettled, Has.Count.EqualTo(1));
                      Assert.That(settlement.Value.SettledTableNames, Is.EqualTo(new[] { "Tisch 12" }));
                      Assert.That(bratwurst.SettledAtUtc, Is.EqualTo(_now));
                      Assert.That(_transactionRunner.Committed, Is.True);
                    });

    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task SettleAsync_AnItemThatIsNoLongerThere_RefusesTheSettlementAndRollsTheTransactionBack()
  {
    Result<SettlementResult, SettlementFailure> settlement = await _service.SettleAsync([Line(Guid.NewGuid(), 350)], _collectingWaiter, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.False);
                      Assert.That(settlement.Failure.Reason, Is.EqualTo(SettlementFailureReason.UnknownOrderItemId));
                      Assert.That(_transactionRunner.Committed, Is.False);
                    });

    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task SettleAsync_AnItemWhoseOrderCannotBeFound_RefusesTheSettlementBecauseItsTableIsUnknown()
  {
    var bratwurst = OpenItem(350);
    A.CallTo(() => _repository.FindForSettlementAsync(A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<OrderItem>>([bratwurst]));

    Result<SettlementResult, SettlementFailure> settlement = await _service.SettleAsync([Line(bratwurst, 350)], _collectingWaiter, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsSuccess, Is.False);
                      Assert.That(settlement.Failure.Reason, Is.EqualTo(SettlementFailureReason.UnknownOrderItemId));
                      Assert.That(bratwurst.SettledAtUtc, Is.Null);
                    });
  }

  private void GivenTheTableHolds(string tableName, params OrderItem[] items)
  {
    foreach (var item in items)
      PutAtTable(tableName, item);

    A.CallTo(() => _repository.FindForSettlementAsync(A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<OrderItem>>(items.ToList()));
  }

  private void PutAtTable(string tableName, OrderItem item)
  {
    Order order = new()
                  {
                    Id = Guid.NewGuid(),
                    ClientOrderId = Guid.NewGuid(),
                    FestivalId = RunningFestival().Id,
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

  private Festival RunningFestival()
  {
    return new()
           {
             Id = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001"),
             Name = "Sommerfest",
             StartsAtUtc = _earlier,
             EndsAtUtc = _now.AddHours(5),
             NextOrderNumber = 1,
             IsHidden = false
           };
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

  private SettleLineRequest Line(Guid orderItemId, int? paidPriceCents, string? paymentNotice = null)
  {
    return new()
           {
             OrderItemId = orderItemId,
             PaidPriceCents = paidPriceCents,
             PaymentNotice = paymentNotice
           };
  }

  private IReadOnlyCollection<OrderItem> AtOneTable(params OrderItem[] items)
  {
    return At("Tisch 12", items);
  }

  private IReadOnlyCollection<OrderItem> At(string tableName, params OrderItem[] items)
  {
    foreach (var item in items)
      PutAtTable(tableName, item);

    return items.ToList();
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
