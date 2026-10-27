using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class TableProgressTest
{
  private readonly TableProgress _service = new(new());

  private readonly OrderItemProgress _untouched = new(OrderItemState.Open, false);
  private readonly OrderItemProgress _handedOut = new(OrderItemState.HandedOut, false);
  private readonly OrderItemProgress _settled = new(OrderItemState.Open, true);
  private readonly OrderItemProgress _cancelled = new(OrderItemState.Cancelled, false);
  private readonly OrderItemProgress _settledAndCancelled = new(OrderItemState.CancellationAcknowledged, true);

  [Test]
  public void HandOutProgressOf_ATableWithoutOrders_IsAll()
  {
    Assert.That(_service.HandOutProgressOf(TableWith()), Is.EqualTo(ShareState.All));
  }

  [Test]
  public void HandOutProgressOf_AnOrderHandedOutAnUntouchedOneAndAPartlyHandedOutOne_IsSome()
  {
    var table = TableWith(OrderOf(_handedOut, _handedOut), OrderOf(_untouched), OrderOf(_handedOut, _untouched));

    Assert.That(_service.HandOutProgressOf(table), Is.EqualTo(ShareState.Some));
  }

  [Test]
  public void HandOutProgressOf_OnlyTheUntouchedOrderLeftBesideHandedOutOnes_IsNone()
  {
    var table = TableWith(OrderOf(_handedOut, _handedOut), OrderOf(_untouched), OrderOf(_handedOut, _handedOut));

    Assert.That(_service.HandOutProgressOf(table), Is.EqualTo(ShareState.None));
  }

  [Test]
  public void HandOutProgressOf_EveryOrderHandedOut_IsAll()
  {
    var table = TableWith(OrderOf(_handedOut, _handedOut), OrderOf(_handedOut), OrderOf(_handedOut, _handedOut));

    Assert.That(_service.HandOutProgressOf(table), Is.EqualTo(ShareState.All));
  }

  [Test]
  public void HandOutProgressOf_ANewOrderAfterEverythingWasHandedOut_IsNone()
  {
    var table = TableWith(OrderOf(_handedOut, _handedOut), OrderOf(_handedOut), OrderOf(_handedOut, _handedOut), OrderOf(_untouched));

    Assert.That(_service.HandOutProgressOf(table), Is.EqualTo(ShareState.None));
  }

  [Test]
  public void HandOutProgressOf_EverythingSettledButNothingHandedOut_IsNone()
  {
    var table = TableWith(OrderOf(_settled, _settled));

    Assert.That(_service.HandOutProgressOf(table), Is.EqualTo(ShareState.None));
  }

  [Test]
  public void HandOutProgressOf_AnOrderWhoseItemsAreAllCancelledBesideAHandedOutOne_IsAll()
  {
    var table = TableWith(OrderOf(_cancelled, _cancelled), OrderOf(_handedOut));

    Assert.That(_service.HandOutProgressOf(table), Is.EqualTo(ShareState.All));
  }

  [Test]
  public void HandOutProgressOf_AHandedOutItemBesideACancelledOne_LeavesTheCancelledOneOut()
  {
    var table = TableWith(OrderOf(_handedOut, _cancelled), OrderOf(_untouched, _cancelled));

    Assert.That(_service.HandOutProgressOf(table), Is.EqualTo(ShareState.None));
  }

  [Test]
  public void HandOutProgressOf_NullTable_ThrowsArgumentNullException()
  {
    Assert.That(() => _service.HandOutProgressOf(null!), Throws.ArgumentNullException);
  }

  [Test]
  public void SettlementProgressOf_ATableWithoutOrders_IsAll()
  {
    Assert.That(_service.SettlementProgressOf(TableWith()), Is.EqualTo(ShareState.All));
  }

  [Test]
  public void SettlementProgressOf_AnOrderSettledAnUnsettledOneAndAPartlySettledOne_IsSome()
  {
    var table = TableWith(OrderOf(_settled, _settled), OrderOf(_untouched), OrderOf(_settled, _untouched));

    Assert.That(_service.SettlementProgressOf(table), Is.EqualTo(ShareState.Some));
  }

  [Test]
  public void SettlementProgressOf_OnlyTheUnsettledOrderLeftBesideSettledOnes_IsNone()
  {
    var table = TableWith(OrderOf(_settled, _settled), OrderOf(_untouched), OrderOf(_settled, _settled));

    Assert.That(_service.SettlementProgressOf(table), Is.EqualTo(ShareState.None));
  }

  [Test]
  public void SettlementProgressOf_EveryOrderSettled_IsAll()
  {
    var table = TableWith(OrderOf(_settled, _settled), OrderOf(_settled), OrderOf(_settled, _settled));

    Assert.That(_service.SettlementProgressOf(table), Is.EqualTo(ShareState.All));
  }

  [Test]
  public void SettlementProgressOf_ANewOrderAfterEverythingWasSettled_IsNone()
  {
    var table = TableWith(OrderOf(_settled, _settled), OrderOf(_settled), OrderOf(_untouched));

    Assert.That(_service.SettlementProgressOf(table), Is.EqualTo(ShareState.None));
  }

  [Test]
  public void SettlementProgressOf_EverythingHandedOutButNothingSettled_IsNone()
  {
    var table = TableWith(OrderOf(_handedOut, _handedOut));

    Assert.That(_service.SettlementProgressOf(table), Is.EqualTo(ShareState.None));
  }

  [Test]
  public void SettlementProgressOf_ASettledItemBesideACancelledUnsettledOne_IsAll()
  {
    var table = TableWith(OrderOf(_settled, _cancelled), OrderOf(_settledAndCancelled));

    Assert.That(_service.SettlementProgressOf(table), Is.EqualTo(ShareState.All));
  }

  [Test]
  public void SettlementProgressOf_NullTable_ThrowsArgumentNullException()
  {
    Assert.That(() => _service.SettlementProgressOf(null!), Throws.ArgumentNullException);
  }

  private ShownTable TableWith(params IReadOnlyList<OrderItemProgress>[] itemProgressPerOrder)
  {
    return new(Guid.NewGuid(),
               "Tisch 3",
               new()
               {
                 X = 0,
                 Y = 0,
                 Width = 80,
                 Height = 40,
                 Rotation = 0
               },
               itemProgressPerOrder);
  }

  private IReadOnlyList<OrderItemProgress> OrderOf(params OrderItemProgress[] items)
  {
    return items;
  }
}
