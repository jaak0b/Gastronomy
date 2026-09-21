using GastronomyApp.Contracts.Enums;
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
    var stationOrder = StationOrderWith(OpenItem(), OpenItem());
    var bratwurst = stationOrder.Items[0];
    var beer = stationOrder.Items[1];

    Result<IReadOnlyList<StationOrder>, FulfillmentFailure> outcome = _service.Fulfill([
                                                                                         bratwurst.Id,
                                                                                         beer.Id
                                                                                       ],
                                                                                       stationOrder.Items,
                                                                                       _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(outcome.IsSuccess, Is.True);
                      Assert.That(bratwurst.FulfilledAtUtc, Is.EqualTo(_now));
                      Assert.That(beer.FulfilledAtUtc, Is.EqualTo(_now));
                    });
  }

  [Test]
  public void Fulfill_ItemsOfTwoStationOrders_NamesEachStationOrderOnce()
  {
    var first = StationOrderWith(OpenItem(), OpenItem());
    var second = StationOrderWith(OpenItem());

    Result<IReadOnlyList<StationOrder>, FulfillmentFailure> outcome = _service.Fulfill([
                                                                                         first.Items[0].Id,
                                                                                         first.Items[1].Id,
                                                                                         second.Items[0].Id
                                                                                       ],
                                                                                       first.Items.Concat(second.Items).ToList(),
                                                                                       _now);

    Assert.That(outcome.Value.Select(stationOrder => stationOrder.Id),
                Is.EqualTo(new[]
                           {
                             first.Id,
                             second.Id
                           }));
  }

  [Test]
  public void Fulfill_AnItemAlreadyFulfilled_KeepsItsExistingTimestamp()
  {
    var stationOrder = StationOrderWith(FulfilledItem(_earlier), OpenItem());
    var bratwurst = stationOrder.Items[0];
    var beer = stationOrder.Items[1];

    Result<IReadOnlyList<StationOrder>, FulfillmentFailure> outcome = _service.Fulfill([
                                                                                         bratwurst.Id,
                                                                                         beer.Id
                                                                                       ],
                                                                                       stationOrder.Items,
                                                                                       _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(outcome.IsSuccess, Is.True);
                      Assert.That(bratwurst.FulfilledAtUtc, Is.EqualTo(_earlier));
                      Assert.That(beer.FulfilledAtUtc, Is.EqualTo(_now));
                    });
  }

  [Test]
  public void Fulfill_TheSameItemNamedTwice_NamesItsStationOrderOnce()
  {
    var stationOrder = StationOrderWith(OpenItem());
    var bratwurst = stationOrder.Items[0];

    Result<IReadOnlyList<StationOrder>, FulfillmentFailure> outcome = _service.Fulfill([
                                                                                         bratwurst.Id,
                                                                                         bratwurst.Id
                                                                                       ],
                                                                                       stationOrder.Items,
                                                                                       _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(outcome.IsSuccess, Is.True);
                      Assert.That(outcome.Value, Has.Count.EqualTo(1));
                    });
  }

  [Test]
  public void Fulfill_AnIdThatIsNotAmongTheKnownItems_IsRefusedNamingItAndChangesNothing()
  {
    var stationOrder = StationOrderWith(OpenItem());
    var bratwurst = stationOrder.Items[0];
    var strangerId = Guid.NewGuid();

    Result<IReadOnlyList<StationOrder>, FulfillmentFailure> outcome = _service.Fulfill([
                                                                                         bratwurst.Id,
                                                                                         strangerId
                                                                                       ],
                                                                                       stationOrder.Items,
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
  public void Fulfill_AnItemAnotherTapHadAlreadyFinished_StillNamesItsStationOrder()
  {
    var stationOrder = StationOrderWith(FulfilledItem(_earlier));
    var bratwurst = stationOrder.Items[0];

    Result<IReadOnlyList<StationOrder>, FulfillmentFailure> outcome = _service.Fulfill([bratwurst.Id], stationOrder.Items, _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(outcome.IsSuccess, Is.True);
                      Assert.That(outcome.Value.Select(touched => touched.Id), Is.EqualTo(new[] { stationOrder.Id }));
                      Assert.That(bratwurst.FulfilledAtUtc, Is.EqualTo(_earlier));
                    });
  }

  [Test]
  public void Unfulfill_FulfilledItems_ClearsTheTimestampOnEveryOneOfThem()
  {
    var stationOrder = StationOrderWith(FulfilledItem(_earlier), FulfilledItem(_now));
    var bratwurst = stationOrder.Items[0];
    var beer = stationOrder.Items[1];

    Result<IReadOnlyList<StationOrder>, FulfillmentFailure> outcome = _service.Unfulfill([
                                                                                           bratwurst.Id,
                                                                                           beer.Id
                                                                                         ],
                                                                                         stationOrder.Items);

    Assert.Multiple(() =>
                    {
                      Assert.That(outcome.IsSuccess, Is.True);
                      Assert.That(outcome.Value.Select(touched => touched.Id), Is.EqualTo(new[] { stationOrder.Id }));
                      Assert.That(bratwurst.FulfilledAtUtc, Is.Null);
                      Assert.That(beer.FulfilledAtUtc, Is.Null);
                    });
  }

  [Test]
  public void Unfulfill_AnOpenItem_IsRefusedNamingItAndChangesNothing()
  {
    var stationOrder = StationOrderWith(FulfilledItem(_earlier), OpenItem());
    var bratwurst = stationOrder.Items[0];
    var beer = stationOrder.Items[1];

    Result<IReadOnlyList<StationOrder>, FulfillmentFailure> outcome = _service.Unfulfill([
                                                                                           bratwurst.Id,
                                                                                           beer.Id
                                                                                         ],
                                                                                         stationOrder.Items);

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
    var stationOrder = StationOrderWith(FulfilledItem(_earlier));
    var bratwurst = stationOrder.Items[0];
    var strangerId = Guid.NewGuid();

    Result<IReadOnlyList<StationOrder>, FulfillmentFailure> outcome = _service.Unfulfill([
                                                                                           bratwurst.Id,
                                                                                           strangerId
                                                                                         ],
                                                                                         stationOrder.Items);

    Assert.Multiple(() =>
                    {
                      Assert.That(outcome.IsSuccess, Is.False);
                      Assert.That(outcome.Failure.Reason, Is.EqualTo(FulfillmentFailureReason.UnknownOrderItemId));
                      Assert.That(outcome.Failure.OffendingOrderItemId, Is.EqualTo(strangerId));
                      Assert.That(bratwurst.FulfilledAtUtc, Is.EqualTo(_earlier));
                    });
  }

  private StationOrder StationOrderWith(params OrderItem[] items)
  {
    StationOrder stationOrder = new()
                                {
                                  Id = Guid.NewGuid(),
                                  OrderId = Guid.NewGuid(),
                                  FestivalId = Guid.NewGuid(),
                                  StationId = Guid.NewGuid(),
                                  StationOrderNumber = 1,
                                  DeliveryMode = DeliveryMode.Together
                                };

    foreach (var item in items)
    {
      item.StationOrderId = stationOrder.Id;
      item.StationOrder = stationOrder;
      stationOrder.Items.Add(item);
    }

    return stationOrder;
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
