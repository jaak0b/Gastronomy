using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class OrderItemFulfillmentServiceTest
{
  [SetUp]
  public void SetUp()
  {
    _service = new();
  }

  private readonly DateTime _earlier = new(2026, 9, 5, 18, 0, 0, DateTimeKind.Utc);
  private readonly DateTime _now = new(2026, 9, 5, 18, 7, 0, DateTimeKind.Utc);

  private OrderItemFulfillmentService _service = null!;

  [Test]
  public void Fulfill_OpenItems_StampsTheClockOnEveryOneOfThem()
  {
    var bratwurst = OpenItem();
    var beer = OpenItem();

    Result<FulfillmentResult, FulfillmentFailure> outcome = _service.Fulfill(new()
                                                                             {
                                                                               OrderItemIds =
                                                                               [
                                                                                 bratwurst.Id,
                                                                                 beer.Id
                                                                               ]
                                                                             },
                                                                             [
                                                                               bratwurst,
                                                                               beer
                                                                             ],
                                                                             _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(outcome.IsSuccess, Is.True);
                      Assert.That(outcome.Value.ChangedItems, Has.Count.EqualTo(2));
                      Assert.That(outcome.Value.AlreadyFulfilled, Is.Empty);
                      Assert.That(bratwurst.FulfilledAtUtc, Is.EqualTo(_now));
                      Assert.That(beer.FulfilledAtUtc, Is.EqualTo(_now));
                    });
  }

  [Test]
  public void Fulfill_AnItemAlreadyFulfilled_KeepsItsExistingTimestamp()
  {
    var bratwurst = FulfilledItem(_earlier);
    var beer = OpenItem();

    Result<FulfillmentResult, FulfillmentFailure> outcome = _service.Fulfill(new()
                                                                             {
                                                                               OrderItemIds =
                                                                               [
                                                                                 bratwurst.Id,
                                                                                 beer.Id
                                                                               ]
                                                                             },
                                                                             [
                                                                               bratwurst,
                                                                               beer
                                                                             ],
                                                                             _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(outcome.IsSuccess, Is.True);
                      Assert.That(outcome.Value.ChangedItems, Has.Count.EqualTo(1));
                      Assert.That(outcome.Value.ChangedItems[0], Is.SameAs(beer));
                      Assert.That(outcome.Value.AlreadyFulfilled, Has.Count.EqualTo(1));
                      Assert.That(outcome.Value.AlreadyFulfilled[0], Is.SameAs(bratwurst));
                      Assert.That(bratwurst.FulfilledAtUtc, Is.EqualTo(_earlier));
                    });
  }

  [Test]
  public void Fulfill_TheSameItemNamedTwice_ChangesItOnce()
  {
    var bratwurst = OpenItem();

    Result<FulfillmentResult, FulfillmentFailure> outcome = _service.Fulfill(new()
                                                                             {
                                                                               OrderItemIds =
                                                                               [
                                                                                 bratwurst.Id,
                                                                                 bratwurst.Id
                                                                               ]
                                                                             },
                                                                             [bratwurst],
                                                                             _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(outcome.IsSuccess, Is.True);
                      Assert.That(outcome.Value.ChangedItems, Has.Count.EqualTo(1));
                    });
  }

  [Test]
  public void Fulfill_AnIdThatIsNotAmongTheKnownItems_IsRefusedNamingItAndChangesNothing()
  {
    var bratwurst = OpenItem();
    var strangerId = Guid.NewGuid();

    Result<FulfillmentResult, FulfillmentFailure> outcome = _service.Fulfill(new()
                                                                             {
                                                                               OrderItemIds =
                                                                               [
                                                                                 bratwurst.Id,
                                                                                 strangerId
                                                                               ]
                                                                             },
                                                                             [bratwurst],
                                                                             _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(outcome.IsSuccess, Is.False);
                      Assert.That(outcome.Failure.Reason, Is.EqualTo(FulfillmentFailureReason.UnknownOrderItemId));
                      Assert.That(outcome.Failure.OffendingOrderItemId, Is.EqualTo(strangerId));
                      Assert.That(bratwurst.FulfilledAtUtc, Is.Null);
                    });
  }

  [Test]
  public void Fulfill_NothingSelected_IsRefused()
  {
    Result<FulfillmentResult, FulfillmentFailure> outcome = _service.Fulfill(new() { OrderItemIds = [] }, [], _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(outcome.IsSuccess, Is.False);
                      Assert.That(outcome.Failure.Reason, Is.EqualTo(FulfillmentFailureReason.NoItemsSelected));
                    });
  }

  [Test]
  public void Fulfill_TheSameItemAlreadyFulfilledTwice_AddsNoSecondTimestamp()
  {
    var bratwurst = FulfilledItem(_earlier);

    Result<FulfillmentResult, FulfillmentFailure> outcome = _service.Fulfill(new() { OrderItemIds = [bratwurst.Id] }, [bratwurst], _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(outcome.IsSuccess, Is.True);
                      Assert.That(outcome.Value.ChangedItems, Is.Empty);
                      Assert.That(outcome.Value.AlreadyFulfilled, Has.Count.EqualTo(1));
                      Assert.That(bratwurst.FulfilledAtUtc, Is.EqualTo(_earlier));
                    });
  }

  [Test]
  public void Unfulfill_FulfilledItems_ClearsTheTimestampOnEveryOneOfThem()
  {
    var bratwurst = FulfilledItem(_earlier);
    var beer = FulfilledItem(_now);

    Result<FulfillmentResult, FulfillmentFailure> outcome = _service.Unfulfill(new()
                                                                               {
                                                                                 OrderItemIds =
                                                                                 [
                                                                                   bratwurst.Id,
                                                                                   beer.Id
                                                                                 ]
                                                                               },
                                                                               [
                                                                                 bratwurst,
                                                                                 beer
                                                                               ]);

    Assert.Multiple(() =>
                    {
                      Assert.That(outcome.IsSuccess, Is.True);
                      Assert.That(outcome.Value.ChangedItems, Has.Count.EqualTo(2));
                      Assert.That(outcome.Value.AlreadyFulfilled, Is.Empty);
                      Assert.That(bratwurst.FulfilledAtUtc, Is.Null);
                      Assert.That(beer.FulfilledAtUtc, Is.Null);
                    });
  }

  [Test]
  public void Unfulfill_AnOpenItem_IsRefusedNamingItAndChangesNothing()
  {
    var bratwurst = FulfilledItem(_earlier);
    var beer = OpenItem();

    Result<FulfillmentResult, FulfillmentFailure> outcome = _service.Unfulfill(new()
                                                                               {
                                                                                 OrderItemIds =
                                                                                 [
                                                                                   bratwurst.Id,
                                                                                   beer.Id
                                                                                 ]
                                                                               },
                                                                               [
                                                                                 bratwurst,
                                                                                 beer
                                                                               ]);

    Assert.Multiple(() =>
                    {
                      Assert.That(outcome.IsSuccess, Is.False);
                      Assert.That(outcome.Failure.Reason, Is.EqualTo(FulfillmentFailureReason.ItemNotFulfilled));
                      Assert.That(outcome.Failure.OffendingOrderItemId, Is.EqualTo(beer.Id));
                      Assert.That(bratwurst.FulfilledAtUtc, Is.EqualTo(_earlier));
                    });
  }

  [Test]
  public void Unfulfill_AnIdThatIsNotAmongTheKnownItems_IsRefusedNamingIt()
  {
    var bratwurst = FulfilledItem(_earlier);
    var strangerId = Guid.NewGuid();

    Result<FulfillmentResult, FulfillmentFailure> outcome = _service.Unfulfill(new()
                                                                               {
                                                                                 OrderItemIds =
                                                                                 [
                                                                                   bratwurst.Id,
                                                                                   strangerId
                                                                                 ]
                                                                               },
                                                                               [bratwurst]);

    Assert.Multiple(() =>
                    {
                      Assert.That(outcome.IsSuccess, Is.False);
                      Assert.That(outcome.Failure.Reason, Is.EqualTo(FulfillmentFailureReason.UnknownOrderItemId));
                      Assert.That(outcome.Failure.OffendingOrderItemId, Is.EqualTo(strangerId));
                      Assert.That(bratwurst.FulfilledAtUtc, Is.EqualTo(_earlier));
                    });
  }

  [Test]
  public void Unfulfill_NothingSelected_IsRefused()
  {
    Result<FulfillmentResult, FulfillmentFailure> outcome = _service.Unfulfill(new() { OrderItemIds = [] }, []);

    Assert.Multiple(() =>
                    {
                      Assert.That(outcome.IsSuccess, Is.False);
                      Assert.That(outcome.Failure.Reason, Is.EqualTo(FulfillmentFailureReason.NoItemsSelected));
                    });
  }

  private OrderItem OpenItem()
  {
    return new()
           {
             Id = Guid.NewGuid(),
             StationOrderId = Guid.NewGuid(),
             CatalogItemId = Guid.NewGuid(),
             ItemName = "Artikel",
             UnitPriceCents = 350
           };
  }

  private OrderItem FulfilledItem(DateTime fulfilledAtUtc)
  {
    var item = OpenItem();
    item.FulfilledAtUtc = fulfilledAtUtc;

    return item;
  }
}
