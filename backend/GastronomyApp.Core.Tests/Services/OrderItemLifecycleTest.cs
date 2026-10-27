using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class OrderItemLifecycleTest
{
  private readonly OrderItemLifecycle _lifecycle = new();
  private readonly DateTime _placedAtUtc = new(2026, 9, 5, 19, 5, 0, DateTimeKind.Utc);
  private readonly DateTime _handedOutAtUtc = new(2026, 9, 5, 19, 20, 0, DateTimeKind.Utc);
  private readonly DateTime _putBackAtUtc = new(2026, 9, 5, 19, 21, 0, DateTimeKind.Utc);
  private readonly DateTime _cancelledAtUtc = new(2026, 9, 5, 19, 25, 0, DateTimeKind.Utc);
  private readonly DateTime _acknowledgedAtUtc = new(2026, 9, 5, 19, 26, 0, DateTimeKind.Utc);
  private readonly OrderItemStatusAuthor _waiter = new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"), null);
  private readonly OrderItemStatusAuthor _kitchen = new(null, Guid.Parse("cccccccc-0000-0000-0000-000000000001"));

  [Test]
  public void AppendStatus_ANewItemOpenedByItsWaiter_AddsAFirstOpenStatusThatIsNotYetCurrent()
  {
    var item = NewItem();

    _lifecycle.AppendStatus(item, OrderItemState.Open, _waiter, _placedAtUtc);

    var status = item.Statuses.Single();

    Assert.Multiple(() =>
                    {
                      Assert.That(status.Status, Is.EqualTo(OrderItemState.Open));
                      Assert.That(status.OrderItemId, Is.EqualTo(item.Id));
                      Assert.That(status.PreviousStatusId, Is.Null);
                      Assert.That(status.CreatedAtUtc, Is.EqualTo(_placedAtUtc));
                      Assert.That(status.CreatedByStaffMemberId, Is.EqualTo(_waiter.StaffMemberId));
                      Assert.That(status.CreatedByStationId, Is.Null);
                      Assert.That(item.CurrentStatus, Is.Null);
                    });
  }

  [Test]
  public void PointAtFirstStatus_AFirstStatusAdded_MakesItTheCurrentOne()
  {
    var item = NewItem();
    _lifecycle.AppendStatus(item, OrderItemState.Open, _waiter, _placedAtUtc);

    _lifecycle.PointAtFirstStatus(item);

    Assert.That(item.CurrentStatus, Is.SameAs(item.Statuses.Single()));
  }

  [Test]
  public void PointAtFirstStatus_AnItemWithoutAnyStatus_Throws()
  {
    Assert.That(() => _lifecycle.PointAtFirstStatus(NewItem()), Throws.InvalidOperationException);
  }

  [Test]
  public void PointAtFirstStatus_AnItemWhoseFirstStatusIsAlreadyCurrent_Throws()
  {
    var item = OpenItem();

    Assert.That(() => _lifecycle.PointAtFirstStatus(item), Throws.InvalidOperationException);
  }

  [Test]
  public void AppendStatus_AnOpenItemHandedOutByTheStation_AddsAHandedOutStatusAfterTheOpenOneAndMakesItCurrent()
  {
    var item = OpenItem();
    var openStatus = item.CurrentStatus!;

    _lifecycle.AppendStatus(item, OrderItemState.HandedOut, _kitchen, _handedOutAtUtc);

    Assert.Multiple(() =>
                    {
                      Assert.That(item.Statuses, Has.Count.EqualTo(2));
                      Assert.That(item.CurrentStatus!.Status, Is.EqualTo(OrderItemState.HandedOut));
                      Assert.That(item.CurrentStatus.PreviousStatusId, Is.EqualTo(openStatus.Id));
                      Assert.That(item.CurrentStatus.PreviousStatus, Is.SameAs(openStatus));
                      Assert.That(item.CurrentStatus.CreatedByStationId, Is.EqualTo(_kitchen.StationId));
                      Assert.That(item.CurrentStatus.CreatedByStaffMemberId, Is.Null);
                      Assert.That(item.CurrentStatus.CreatedAtUtc, Is.EqualTo(_handedOutAtUtc));
                    });
  }

  [Test]
  public void AppendStatus_AHandedOutItemPutBackByTheStation_AddsAnOpenStatusAfterTheHandedOutOne()
  {
    var item = OpenItem();
    _lifecycle.AppendStatus(item, OrderItemState.HandedOut, _kitchen, _handedOutAtUtc);
    var handedOut = item.CurrentStatus!;

    _lifecycle.AppendStatus(item, OrderItemState.Open, _kitchen, _putBackAtUtc);

    Assert.Multiple(() =>
                    {
                      Assert.That(item.Statuses, Has.Count.EqualTo(3));
                      Assert.That(item.CurrentStatus!.Status, Is.EqualTo(OrderItemState.Open));
                      Assert.That(item.CurrentStatus.PreviousStatusId, Is.EqualTo(handedOut.Id));
                      Assert.That(item.CurrentStatus.CreatedByStationId, Is.EqualTo(_kitchen.StationId));
                      Assert.That(item.CurrentStatus.CreatedAtUtc, Is.EqualTo(_putBackAtUtc));
                    });
  }

  [Test]
  public void CanAppendStatus_EachStateAndTarget_AllowsOnlyTheChangesOfTheLifecycle()
  {
    var newItem = NewItem();
    var openItem = OpenItem();
    var handedOutItem = OpenItem();
    _lifecycle.AppendStatus(handedOutItem, OrderItemState.HandedOut, _kitchen, _handedOutAtUtc);

    Assert.Multiple(() =>
                    {
                      Assert.That(_lifecycle.CanAppendStatus(newItem, OrderItemState.Open), Is.True);
                      Assert.That(_lifecycle.CanAppendStatus(newItem, OrderItemState.HandedOut), Is.False);
                      Assert.That(_lifecycle.CanAppendStatus(openItem, OrderItemState.HandedOut), Is.True);
                      Assert.That(_lifecycle.CanAppendStatus(openItem, OrderItemState.Open), Is.False);
                      Assert.That(_lifecycle.CanAppendStatus(handedOutItem, OrderItemState.Open), Is.True);
                      Assert.That(_lifecycle.CanAppendStatus(handedOutItem, OrderItemState.HandedOut), Is.False);
                    });
  }

  [Test]
  public void AppendStatus_AnOpenItemCancelledByAWaiter_AddsACancelledStatusAfterTheOpenOneAndMakesItCurrent()
  {
    var item = OpenItem();
    var openStatus = item.CurrentStatus!;

    _lifecycle.AppendStatus(item, OrderItemState.Cancelled, _waiter, _cancelledAtUtc);

    Assert.Multiple(() =>
                    {
                      Assert.That(item.CurrentStatus!.Status, Is.EqualTo(OrderItemState.Cancelled));
                      Assert.That(item.CurrentStatus.PreviousStatus, Is.SameAs(openStatus));
                      Assert.That(item.CurrentStatus.CreatedByStaffMemberId, Is.EqualTo(_waiter.StaffMemberId));
                      Assert.That(item.CurrentStatus.CreatedByStationId, Is.Null);
                      Assert.That(item.CurrentStatus.CreatedAtUtc, Is.EqualTo(_cancelledAtUtc));
                    });
  }

  [Test]
  public void AppendStatus_AHandedOutItemCancelledByAWaiter_AddsACancelledStatusAfterTheHandedOutOne()
  {
    var item = HandedOutItem();
    var handedOut = item.CurrentStatus!;

    _lifecycle.AppendStatus(item, OrderItemState.Cancelled, _waiter, _cancelledAtUtc);

    Assert.Multiple(() =>
                    {
                      Assert.That(item.CurrentStatus!.Status, Is.EqualTo(OrderItemState.Cancelled));
                      Assert.That(item.CurrentStatus.PreviousStatus, Is.SameAs(handedOut));
                    });
  }

  [Test]
  public void AppendStatus_ACancellationOfAnOpenItemAcknowledgedByTheStation_AddsTheAcknowledgementAfterTheCancellation()
  {
    var item = CancelledItem(OpenItem());
    var cancelled = item.CurrentStatus!;

    _lifecycle.AppendStatus(item, OrderItemState.CancellationAcknowledged, _kitchen, _acknowledgedAtUtc);

    Assert.Multiple(() =>
                    {
                      Assert.That(item.Statuses, Has.Count.EqualTo(3));
                      Assert.That(item.CurrentStatus!.Status, Is.EqualTo(OrderItemState.CancellationAcknowledged));
                      Assert.That(item.CurrentStatus.PreviousStatus, Is.SameAs(cancelled));
                      Assert.That(item.CurrentStatus.CreatedByStationId, Is.EqualTo(_kitchen.StationId));
                      Assert.That(item.CurrentStatus.CreatedAtUtc, Is.EqualTo(_acknowledgedAtUtc));
                    });
  }

  [Test]
  public void CanAppendStatus_CancelledAndAcknowledgedItems_AllowOnlyTheAcknowledgementOfACancellationWhileOpen()
  {
    var cancelledWhileOpen = CancelledItem(OpenItem());
    var cancelledAfterHandOut = CancelledItem(HandedOutItem());
    var acknowledged = CancelledItem(OpenItem());
    _lifecycle.AppendStatus(acknowledged, OrderItemState.CancellationAcknowledged, _kitchen, _acknowledgedAtUtc);
    var putBackAndCancelled = HandedOutItem();
    _lifecycle.AppendStatus(putBackAndCancelled, OrderItemState.Open, _kitchen, _putBackAtUtc);
    _lifecycle.AppendStatus(putBackAndCancelled, OrderItemState.Cancelled, _waiter, _cancelledAtUtc);

    Assert.Multiple(() =>
                    {
                      Assert.That(_lifecycle.CanAppendStatus(OpenItem(), OrderItemState.Cancelled), Is.True);
                      Assert.That(_lifecycle.CanAppendStatus(HandedOutItem(), OrderItemState.Cancelled), Is.True);
                      Assert.That(_lifecycle.CanAppendStatus(OpenItem(), OrderItemState.CancellationAcknowledged), Is.False);
                      Assert.That(_lifecycle.CanAppendStatus(cancelledWhileOpen, OrderItemState.CancellationAcknowledged), Is.True);
                      Assert.That(_lifecycle.CanAppendStatus(putBackAndCancelled, OrderItemState.CancellationAcknowledged), Is.True);
                      Assert.That(_lifecycle.CanAppendStatus(cancelledAfterHandOut, OrderItemState.CancellationAcknowledged), Is.False);
                      Assert.That(_lifecycle.CanAppendStatus(cancelledWhileOpen, OrderItemState.Open), Is.False);
                      Assert.That(_lifecycle.CanAppendStatus(cancelledWhileOpen, OrderItemState.HandedOut), Is.False);
                      Assert.That(_lifecycle.CanAppendStatus(cancelledWhileOpen, OrderItemState.Cancelled), Is.False);
                      Assert.That(_lifecycle.CanAppendStatus(acknowledged, OrderItemState.Open), Is.False);
                      Assert.That(_lifecycle.CanAppendStatus(acknowledged, OrderItemState.HandedOut), Is.False);
                      Assert.That(_lifecycle.CanAppendStatus(acknowledged, OrderItemState.Cancelled), Is.False);
                      Assert.That(_lifecycle.CanAppendStatus(acknowledged, OrderItemState.CancellationAcknowledged), Is.False);
                    });
  }

  [Test]
  public void AppendStatus_ACancelledItemCancelledAgain_ThrowsAndAddsNothing()
  {
    var item = CancelledItem(OpenItem());

    Assert.Multiple(() =>
                    {
                      Assert.That(() => _lifecycle.AppendStatus(item, OrderItemState.Cancelled, _waiter, _acknowledgedAtUtc), Throws.InvalidOperationException);
                      Assert.That(item.Statuses, Has.Count.EqualTo(2));
                    });
  }

  [Test]
  public void AppendStatus_AnOpenItemCancelledByAStation_ThrowsArgumentException()
  {
    Assert.That(() => _lifecycle.AppendStatus(OpenItem(), OrderItemState.Cancelled, _kitchen, _cancelledAtUtc), Throws.ArgumentException.With.Property(nameof(ArgumentException.ParamName)).EqualTo("author"));
  }

  [Test]
  public void AppendStatus_ACancellationAcknowledgedByAWaiter_ThrowsArgumentException()
  {
    Assert.That(() => _lifecycle.AppendStatus(CancelledItem(OpenItem()), OrderItemState.CancellationAcknowledged, _waiter, _acknowledgedAtUtc), Throws.ArgumentException);
  }

  [Test]
  public void CheckStatusChange_EachStateAndTarget_NamesWhetherAndWhyTheChangeIsRefused()
  {
    var acknowledged = CancelledItem(OpenItem());
    _lifecycle.AppendStatus(acknowledged, OrderItemState.CancellationAcknowledged, _kitchen, _acknowledgedAtUtc);
    var cancelledAfterHandOut = CancelledItem(HandedOutItem());

    Assert.Multiple(() =>
                    {
                      Assert.That(_lifecycle.CheckStatusChange(OpenItem(), OrderItemState.HandedOut), Is.EqualTo(OrderItemStatusChange.Allowed));
                      Assert.That(_lifecycle.CheckStatusChange(HandedOutItem(), OrderItemState.HandedOut), Is.EqualTo(OrderItemStatusChange.AlreadyInThatState));
                      Assert.That(_lifecycle.CheckStatusChange(OpenItem(), OrderItemState.Open), Is.EqualTo(OrderItemStatusChange.AlreadyInThatState));
                      Assert.That(_lifecycle.CheckStatusChange(acknowledged, OrderItemState.CancellationAcknowledged), Is.EqualTo(OrderItemStatusChange.AlreadyInThatState));
                      Assert.That(_lifecycle.CheckStatusChange(CancelledItem(OpenItem()), OrderItemState.HandedOut), Is.EqualTo(OrderItemStatusChange.ItemIsCancelled));
                      Assert.That(_lifecycle.CheckStatusChange(cancelledAfterHandOut, OrderItemState.Open), Is.EqualTo(OrderItemStatusChange.ItemIsCancelled));
                      Assert.That(_lifecycle.CheckStatusChange(acknowledged, OrderItemState.HandedOut), Is.EqualTo(OrderItemStatusChange.ItemIsCancelled));
                      Assert.That(_lifecycle.CheckStatusChange(cancelledAfterHandOut, OrderItemState.CancellationAcknowledged), Is.EqualTo(OrderItemStatusChange.NotAllowed));
                      Assert.That(_lifecycle.CheckStatusChange(OpenItem(), OrderItemState.CancellationAcknowledged), Is.EqualTo(OrderItemStatusChange.NotAllowed));
                      Assert.That(_lifecycle.CheckStatusChange(CancelledItem(OpenItem()), OrderItemState.CancellationAcknowledged), Is.EqualTo(OrderItemStatusChange.Allowed));
                    });
  }

  [Test]
  public void CheckStatusChange_NullItem_ThrowsArgumentNullException()
  {
    Assert.That(() => _lifecycle.CheckStatusChange(null!, OrderItemState.Open), Throws.ArgumentNullException);
  }

  [Test]
  public void CanAppendStatus_ACancellationReadWithoutTheStatusBeforeIt_ThrowsNamingTheMissingStatus()
  {
    var item = CancelledItem(OpenItem());
    item.CurrentStatus!.PreviousStatus = null;

    Assert.That(() => _lifecycle.CanAppendStatus(item, OrderItemState.CancellationAcknowledged), Throws.InvalidOperationException.With.Message.Contains("OrderItemStatus.PreviousStatus"));
  }

  [Test]
  public void AppendStatus_AnOpenItemOpenedAgain_ThrowsAndAddsNothing()
  {
    var item = OpenItem();

    Assert.Multiple(() =>
                    {
                      Assert.That(() => _lifecycle.AppendStatus(item, OrderItemState.Open, _kitchen, _handedOutAtUtc), Throws.InvalidOperationException);
                      Assert.That(item.Statuses, Has.Count.EqualTo(1));
                    });
  }

  [Test]
  public void AppendStatus_AHandedOutItemHandedOutAgain_ThrowsAndAddsNothing()
  {
    var item = OpenItem();
    _lifecycle.AppendStatus(item, OrderItemState.HandedOut, _kitchen, _handedOutAtUtc);

    Assert.Multiple(() =>
                    {
                      Assert.That(() => _lifecycle.AppendStatus(item, OrderItemState.HandedOut, _kitchen, _putBackAtUtc), Throws.InvalidOperationException);
                      Assert.That(item.Statuses, Has.Count.EqualTo(2));
                    });
  }

  [Test]
  public void AppendStatus_ANewItemHandedOutStraightAway_Throws()
  {
    Assert.That(() => _lifecycle.AppendStatus(NewItem(), OrderItemState.HandedOut, _kitchen, _handedOutAtUtc), Throws.InvalidOperationException);
  }

  [Test]
  public void AppendStatus_AFirstStatusNotYetMadeCurrent_ThrowsBeforeAnotherChange()
  {
    var item = NewItem();
    _lifecycle.AppendStatus(item, OrderItemState.Open, _waiter, _placedAtUtc);

    Assert.That(() => _lifecycle.AppendStatus(item, OrderItemState.HandedOut, _kitchen, _handedOutAtUtc), Throws.InvalidOperationException);
  }

  [Test]
  public void AppendStatus_AnItemWhoseCurrentStatusWasNotLoaded_Throws()
  {
    var item = NewItem();
    item.CurrentStatusId = Guid.NewGuid();

    Assert.That(() => _lifecycle.AppendStatus(item, OrderItemState.HandedOut, _kitchen, _handedOutAtUtc), Throws.InvalidOperationException);
  }

  [Test]
  public void AppendStatus_ANewItemOpenedByAStation_ThrowsArgumentExceptionAndAddsNothing()
  {
    var item = NewItem();

    Assert.Multiple(() =>
                    {
                      Assert.That(() => _lifecycle.AppendStatus(item, OrderItemState.Open, _kitchen, _placedAtUtc), Throws.ArgumentException.With.Property(nameof(ArgumentException.ParamName)).EqualTo("author"));
                      Assert.That(item.Statuses, Is.Empty);
                    });
  }

  [Test]
  public void AppendStatus_AnOpenItemHandedOutByAWaiter_ThrowsArgumentException()
  {
    Assert.That(() => _lifecycle.AppendStatus(OpenItem(), OrderItemState.HandedOut, _waiter, _handedOutAtUtc), Throws.ArgumentException);
  }

  [Test]
  public void AppendStatus_AnAuthorNamingBothAWaiterAndAStation_ThrowsArgumentException()
  {
    OrderItemStatusAuthor both = new(_waiter.StaffMemberId, _kitchen.StationId);

    Assert.That(() => _lifecycle.AppendStatus(OpenItem(), OrderItemState.HandedOut, both, _handedOutAtUtc), Throws.ArgumentException);
  }

  [Test]
  public void AppendStatus_NullItem_ThrowsArgumentNullException()
  {
    Assert.That(() => _lifecycle.AppendStatus(null!, OrderItemState.Open, _waiter, _placedAtUtc), Throws.ArgumentNullException);
  }

  [Test]
  public void AppendStatus_NullAuthor_ThrowsArgumentNullException()
  {
    Assert.That(() => _lifecycle.AppendStatus(NewItem(), OrderItemState.Open, null!, _placedAtUtc), Throws.ArgumentNullException);
  }

  [Test]
  public void CanAppendStatus_NullItem_ThrowsArgumentNullException()
  {
    Assert.That(() => _lifecycle.CanAppendStatus(null!, OrderItemState.Open), Throws.ArgumentNullException);
  }

  [Test]
  public void PointAtFirstStatus_NullItem_ThrowsArgumentNullException()
  {
    Assert.That(() => _lifecycle.PointAtFirstStatus(null!), Throws.ArgumentNullException);
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

  private OrderItem HandedOutItem()
  {
    var item = OpenItem();
    _lifecycle.AppendStatus(item, OrderItemState.HandedOut, _kitchen, _handedOutAtUtc);

    return item;
  }

  private OrderItem CancelledItem(OrderItem item)
  {
    _lifecycle.AppendStatus(item, OrderItemState.Cancelled, _waiter, _cancelledAtUtc);

    return item;
  }

  private OrderItem OpenItem()
  {
    var item = NewItem();
    _lifecycle.AppendStatus(item, OrderItemState.Open, _waiter, _placedAtUtc);
    _lifecycle.PointAtFirstStatus(item);

    return item;
  }
}
