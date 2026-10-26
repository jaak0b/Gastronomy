using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Mapping;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Mapping;

[TestFixture]
public sealed class OrderItemStatesTest
{
  private readonly OrderItemLifecycle _lifecycle = new();
  private readonly DateTime _placedAtUtc = new(2026, 9, 5, 19, 5, 0, DateTimeKind.Utc);
  private readonly DateTime _handedOutAtUtc = new(2026, 9, 5, 19, 20, 0, DateTimeKind.Utc);
  private readonly Guid _kitchenId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

  [Test]
  public void CurrentStatusOf_AnItemReadWithoutItsCurrentStatus_ThrowsNamingTheMissingStatus()
  {
    var item = NewItem();
    item.CurrentStatusId = Guid.NewGuid();

    Assert.That(() => OrderItemStates.CurrentStatusOf(item), Throws.InvalidOperationException.With.Message.Contains("current status"));
  }

  [Test]
  public void CurrentStatusOf_NullItem_ThrowsArgumentNullException()
  {
    Assert.That(() => OrderItemStates.CurrentStatusOf(null!), Throws.ArgumentNullException);
  }

  [Test]
  public void IsHandedOut_AnOpenAndAHandedOutItem_TellsThemApart()
  {
    var open = OpenItem();
    var handedOut = OpenItem();
    _lifecycle.AppendStatus(handedOut, OrderItemState.HandedOut, new(null, _kitchenId), _handedOutAtUtc);

    Assert.Multiple(() =>
                    {
                      Assert.That(OrderItemStates.IsHandedOut(open), Is.False);
                      Assert.That(OrderItemStates.IsHandedOut(handedOut), Is.True);
                    });
  }

  [Test]
  public void HandedOutAtUtcOf_AHandedOutItem_IsTheTimeOfItsCurrentStatus()
  {
    var item = OpenItem();
    _lifecycle.AppendStatus(item, OrderItemState.HandedOut, new(null, _kitchenId), _handedOutAtUtc);

    Assert.That(OrderItemStates.HandedOutAtUtcOf(item), Is.EqualTo(_handedOutAtUtc));
  }

  [Test]
  public void HandedOutAtUtcOf_AnItemPutBack_IsNull()
  {
    var item = OpenItem();
    _lifecycle.AppendStatus(item, OrderItemState.HandedOut, new(null, _kitchenId), _handedOutAtUtc);
    _lifecycle.AppendStatus(item, OrderItemState.Open, new(null, _kitchenId), _handedOutAtUtc.AddMinutes(1));

    Assert.That(OrderItemStates.HandedOutAtUtcOf(item), Is.Null);
  }

  [Test]
  public void EveryPredicate_CompiledForItemsAtEveryStep_AgreesWithIsHandedOutAndTheSettledFlag()
  {
    List<OrderItem> items =
    [
      OpenItem(),
      HandedOutItem(),
      Settled(OpenItem()),
      Settled(HandedOutItem())
    ];
    Func<OrderItem, bool> handedOutPredicate = OrderItemStates.HandedOutPredicate.Compile();
    Func<OrderItem, bool> notHandedOutPredicate = OrderItemStates.NotHandedOutPredicate.Compile();
    Func<OrderItem, bool> notHandedOutOrNotSettledPredicate = OrderItemStates.NotHandedOutOrNotSettledPredicate.Compile();

    Assert.Multiple(() =>
                    {
                      Assert.That(items.Select(handedOutPredicate), Is.EqualTo(items.Select(OrderItemStates.IsHandedOut)));
                      Assert.That(items.Select(notHandedOutPredicate), Is.EqualTo(items.Select(item => !OrderItemStates.IsHandedOut(item))));
                      Assert.That(items.Select(notHandedOutOrNotSettledPredicate), Is.EqualTo(items.Select(item => !OrderItemStates.IsHandedOut(item) || item.SettledAtUtc is null)));
                      Assert.That(items.Select(notHandedOutOrNotSettledPredicate),
                                  Is.EqualTo(new[]
                                             {
                                               true,
                                               true,
                                               true,
                                               false
                                             }));
                    });
  }

  private OrderItem HandedOutItem()
  {
    var item = OpenItem();
    _lifecycle.AppendStatus(item, OrderItemState.HandedOut, new(null, _kitchenId), _handedOutAtUtc);

    return item;
  }

  private OrderItem Settled(OrderItem item)
  {
    item.SettledAtUtc = _handedOutAtUtc.AddMinutes(5);

    return item;
  }

  private OrderItem NewItem()
  {
    return new()
           {
             Id = Guid.NewGuid(),
             StationOrderId = Guid.NewGuid(),
             CatalogItemId = Guid.NewGuid(),
             ItemName = "Bratwurst",
             UnitPriceCents = 350
           };
  }

  private OrderItem OpenItem()
  {
    var item = NewItem();
    _lifecycle.AppendStatus(item, OrderItemState.Open, new(Guid.NewGuid(), null), _placedAtUtc);
    _lifecycle.PointAtFirstStatus(item);

    return item;
  }
}
