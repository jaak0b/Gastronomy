using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class TableServiceTest
{
  private readonly TableService _service = new(new());

  private readonly OrderItemFlags _untouched = new(false, false);
  private readonly OrderItemFlags _handedOut = new(true, false);
  private readonly OrderItemFlags _settled = new(false, true);

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
  public void SettlementProgressOf_NullTable_ThrowsArgumentNullException()
  {
    Assert.That(() => _service.SettlementProgressOf(null!), Throws.ArgumentNullException);
  }

  private ShownTable TableWith(params IReadOnlyList<OrderItemFlags>[] itemFlagsPerOrder)
  {
    return new(new()
               {
                 Id = Guid.NewGuid(),
                 FestivalId = Guid.NewGuid(),
                 AreaId = Guid.NewGuid(),
                 Name = "Tisch 3",
                 IsHidden = false
               },
               itemFlagsPerOrder);
  }

  private IReadOnlyList<OrderItemFlags> OrderOf(params OrderItemFlags[] items)
  {
    return items;
  }
}
