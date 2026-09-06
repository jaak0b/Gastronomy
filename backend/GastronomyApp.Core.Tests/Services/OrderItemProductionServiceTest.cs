using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class OrderItemProductionServiceTest
{
  [SetUp]
  public void SetUp()
  {
    _service = new();
  }

  private readonly DateTime _placedAt = new(2026, 9, 5, 18, 0, 0, DateTimeKind.Utc);
  private readonly DateTime _now = new(2026, 9, 5, 18, 7, 0, DateTimeKind.Utc);

  private OrderItemProductionService _service = null!;

  [Test]
  public void RecordPlacement_NewItem_StartsWaitingWithOneLogRow()
  {
    var item = BareItem();

    _service.RecordPlacement(item, _placedAt);

    Assert.Multiple(() =>
                    {
                      Assert.That(item.ProductionStatus, Is.EqualTo(ProductionStatus.Waiting));
                      Assert.That(item.StatusChanges, Has.Count.EqualTo(1));
                      Assert.That(item.StatusChanges[0].Status, Is.EqualTo(ProductionStatus.Waiting));
                      Assert.That(item.StatusChanges[0].ChangedAtUtc, Is.EqualTo(_placedAt));
                      Assert.That(item.StatusChanges[0].OrderItemId, Is.EqualTo(item.Id));
                      Assert.That(item.StatusChanges[0].Id, Is.Not.EqualTo(Guid.Empty));
                    });
  }

  [Test]
  public void Advance_WaitingItemsToInProduction_MovesThemAndAppendsOneLogRowEach()
  {
    var bratwurst = WaitingItem();
    var beer = WaitingItem();

    var outcome = _service.Advance(RequestFor([bratwurst.Id, beer.Id], ProductionStatus.InProduction),
                                   [bratwurst, beer],
                                   _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(outcome.IsSuccess, Is.True);
                      Assert.That(outcome.Value.ChangedItems, Has.Count.EqualTo(2));
                      Assert.That(bratwurst.ProductionStatus, Is.EqualTo(ProductionStatus.InProduction));
                      Assert.That(beer.ProductionStatus, Is.EqualTo(ProductionStatus.InProduction));
                      Assert.That(bratwurst.StatusChanges, Has.Count.EqualTo(2));
                      Assert.That(bratwurst.StatusChanges[1].Status, Is.EqualTo(ProductionStatus.InProduction));
                      Assert.That(bratwurst.StatusChanges[1].ChangedAtUtc, Is.EqualTo(_now));
                    });
  }

  [Test]
  public void Advance_WaitingItemStraightToFinished_IsAllowed()
  {
    var bratwurst = WaitingItem();

    var outcome = _service.Advance(RequestFor([bratwurst.Id], ProductionStatus.Finished), [bratwurst], _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(outcome.IsSuccess, Is.True);
                      Assert.That(bratwurst.ProductionStatus, Is.EqualTo(ProductionStatus.Finished));
                    });
  }

  [Test]
  public void Advance_FinishedItemBackToInProduction_IsRefusedNamingTheItemAndChangesNothing()
  {
    var bratwurst = WaitingItem();
    var beer = WaitingItem();
    _service.Advance(RequestFor([bratwurst.Id], ProductionStatus.Finished), [bratwurst], _placedAt);

    var outcome = _service.Advance(RequestFor([beer.Id, bratwurst.Id], ProductionStatus.InProduction),
                                   [bratwurst, beer],
                                   _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(outcome.IsSuccess, Is.False);
                      Assert.That(outcome.Failure.Reason, Is.EqualTo(ProductionStatusFailureReason.TransitionNotAllowed));
                      Assert.That(outcome.Failure.OffendingOrderItemId, Is.EqualTo(bratwurst.Id));
                      Assert.That(beer.ProductionStatus, Is.EqualTo(ProductionStatus.Waiting));
                      Assert.That(beer.StatusChanges, Has.Count.EqualTo(1));
                    });
  }

  [Test]
  public void Advance_AnIdThatIsNotAmongTheKnownItems_IsRefusedAndChangesNothing()
  {
    var bratwurst = WaitingItem();
    var strangerId = Guid.NewGuid();

    var outcome = _service.Advance(RequestFor([bratwurst.Id, strangerId], ProductionStatus.Finished),
                                   [bratwurst],
                                   _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(outcome.IsSuccess, Is.False);
                      Assert.That(outcome.Failure.Reason, Is.EqualTo(ProductionStatusFailureReason.UnknownOrderItemId));
                      Assert.That(outcome.Failure.OffendingOrderItemId, Is.EqualTo(strangerId));
                      Assert.That(bratwurst.ProductionStatus, Is.EqualTo(ProductionStatus.Waiting));
                    });
  }

  [Test]
  public void Advance_NothingSelected_IsRefused()
  {
    var outcome = _service.Advance(RequestFor([], ProductionStatus.Finished), [], _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(outcome.IsSuccess, Is.False);
                      Assert.That(outcome.Failure.Reason, Is.EqualTo(ProductionStatusFailureReason.NoItemsSelected));
                    });
  }

  [Test]
  public void Advance_TheSameItemNamedTwice_ChangesItOnce()
  {
    var bratwurst = WaitingItem();

    var outcome = _service.Advance(RequestFor([bratwurst.Id, bratwurst.Id], ProductionStatus.Finished),
                                   [bratwurst],
                                   _now);

    Assert.Multiple(() =>
                    {
                      Assert.That(outcome.IsSuccess, Is.True);
                      Assert.That(outcome.Value.ChangedItems, Has.Count.EqualTo(1));
                      Assert.That(bratwurst.StatusChanges, Has.Count.EqualTo(2));
                    });
  }

  private ProductionStatusChangeRequest RequestFor(IReadOnlyList<Guid> orderItemIds, ProductionStatus target)
  {
    return new()
           {
             OrderItemIds = orderItemIds,
             TargetStatus = target
           };
  }

  private OrderItem WaitingItem()
  {
    var item = BareItem();
    _service.RecordPlacement(item, _placedAt);

    return item;
  }

  private OrderItem BareItem()
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
}
