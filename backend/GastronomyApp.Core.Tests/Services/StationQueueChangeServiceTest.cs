using FakeItEasy;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using GastronomyApp.Core.Tests.TestSupport;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class StationQueueChangeServiceTest
{
  [SetUp]
  public void SetUp()
  {
    _stationOrderRepository = A.Fake<IStationOrderRepository>();
    _stationRepository = A.Fake<IStationRepository>();
    _festivalRepository = A.Fake<IFestivalRepository>();
    _festivalStationRepository = A.Fake<IFestivalStationRepository>();
    _clock = A.Fake<IClock>();

    A.CallTo(() => _clock.UtcNow).Returns(_now);
    A.CallTo(() => _stationRepository.FindByIdAsync(_stationId, A<CancellationToken>._)).Returns(Task.FromResult<Station?>(Kitchen()));
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(RunningFestival()));
    A.CallTo(() => _festivalStationRepository.FindLinkAsync(_festivalId, _stationId, A<CancellationToken>._)).Returns(Task.FromResult<FestivalStation?>(Link()));
    A.CallTo(() => _stationOrderRepository.FindItemsAtStationAsync(A<IReadOnlyCollection<Guid>>._, A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<OrderItem>>([]));
    A.CallTo(() => _stationOrderRepository.FindUnfinishedAtStationAsync(A<Guid>._, A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<QueuedStationOrder>>([]));
    A.CallTo(() => _stationOrderRepository.FindOrderIdsOfStationOrdersAsync(A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<Guid>>([_orderId]));
    A.CallTo(() => _stationOrderRepository.FindFulfillmentCountsAsync(A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._))
   .Returns(Task.FromResult<IReadOnlyList<OrderFulfillmentCounts>>([
                                                                     new()
                                                                     {
                                                                       OrderId = _orderId,
                                                                       ItemCount = 2,
                                                                       FulfilledItemCount = 1
                                                                     }
                                                                   ]));

    StationAtFestivalLookup lookup = new(_stationRepository, _festivalStationRepository, new(_festivalRepository, new(), _clock));

    _service = new(lookup, new(_stationOrderRepository, new(), new(), new RecordingTransactionRunner(), _clock), new(lookup, _stationOrderRepository), new(_stationOrderRepository, new()));
  }

  private readonly DateTime _now = new(2026, 9, 5, 20, 15, 0, DateTimeKind.Utc);
  private readonly Guid _festivalId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
  private readonly Guid _stationId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
  private readonly Guid _orderId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

  private IClock _clock = null!;
  private IFestivalRepository _festivalRepository = null!;
  private IFestivalStationRepository _festivalStationRepository = null!;
  private IStationOrderRepository _stationOrderRepository = null!;
  private IStationRepository _stationRepository = null!;
  private StationQueueChangeService _service = null!;

  [Test]
  public async Task FulfillAsync_AnOpenItem_AnswersWithTheFreshQueueAndTheNewStatusOfItsOrder()
  {
    var bratwurst = OpenItem();
    GivenItemsAtThisStation(bratwurst);

    Result<StationQueueChange, StationQueueFailure> change = await _service.FulfillAsync([bratwurst.Id], _stationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(change.IsSuccess, Is.True);
                      Assert.That(change.Value.Queue.StationId, Is.EqualTo(_stationId));
                      Assert.That(change.Value.OrderStatusChanges, Has.Count.EqualTo(1));
                      Assert.That(change.Value.OrderStatusChanges[0].OrderId, Is.EqualTo(_orderId));
                      Assert.That(change.Value.OrderStatusChanges[0].Status, Is.EqualTo(OrderStatus.PartiallyFulfilled));
                    });
  }

  [Test]
  public async Task FulfillAsync_AnItemAnotherTapHadAlreadyFinished_StillReportsTheStatusOfThatOrder()
  {
    var bratwurst = OpenItem();
    bratwurst.FulfilledAtUtc = _now.AddMinutes(-1);
    GivenItemsAtThisStation(bratwurst);

    Result<StationQueueChange, StationQueueFailure> change = await _service.FulfillAsync([bratwurst.Id], _stationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(change.IsSuccess, Is.True);
                      Assert.That(change.Value.OrderStatusChanges, Has.Count.EqualTo(1));
                    });
  }

  [Test]
  public async Task FulfillAsync_NoFestivalIsRunning_RefusesAndWritesNothing()
  {
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(null));

    Result<StationQueueChange, StationQueueFailure> change = await _service.FulfillAsync([Guid.NewGuid()], _stationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(change.IsSuccess, Is.False);
                      Assert.That(change.Failure.Reason, Is.EqualTo(StationQueueFailureReason.NoRunningFestival));
                    });

    A.CallTo(() => _stationOrderRepository.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public void FulfillAsync_NullSelection_ThrowsArgumentNullException()
  {
    Assert.That(async () => await _service.FulfillAsync(null!, _stationId, TestContext.CurrentContext.CancellationToken), Throws.ArgumentNullException);
  }

  [Test]
  public async Task UnfulfillAsync_AnItemTheStationHandedOut_AnswersWithTheStatusOfItsOrder()
  {
    var bratwurst = OpenItem();
    bratwurst.FulfilledAtUtc = _now.AddMinutes(-1);
    GivenItemsAtThisStation(bratwurst);

    Result<StationQueueChange, StationQueueFailure> change = await _service.UnfulfillAsync([bratwurst.Id], _stationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(change.IsSuccess, Is.True);
                      Assert.That(change.Value.OrderStatusChanges, Has.Count.EqualTo(1));
                    });
  }

  [Test]
  public void UnfulfillAsync_NullSelection_ThrowsArgumentNullException()
  {
    Assert.That(async () => await _service.UnfulfillAsync(null!, _stationId, TestContext.CurrentContext.CancellationToken), Throws.ArgumentNullException);
  }

  [Test]
  public async Task HideFromAsItComesQueueAsync_AnAsItComesOrder_AnswersWithTheQueueAndNoStatusChange()
  {
    var stationOrder = AsItComesStationOrder();
    A.CallTo(() => _stationOrderRepository.FindAtStationAsync(stationOrder.Id, _stationId, _festivalId, A<CancellationToken>._)).Returns(Task.FromResult<StationOrder?>(stationOrder));

    Result<StationQueueChange, StationQueueFailure> change = await _service.HideFromAsItComesQueueAsync(stationOrder.Id, _stationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(change.IsSuccess, Is.True);
                      Assert.That(change.Value.OrderStatusChanges, Is.Empty);
                      Assert.That(stationOrder.IsHiddenFromAsItComesQueue, Is.True);
                    });
  }

  [Test]
  public async Task HideFromAsItComesQueueAsync_TheStationIsNotAtTheRunningFestival_RefusesBeforeItReadsTheOrder()
  {
    A.CallTo(() => _festivalStationRepository.FindLinkAsync(_festivalId, _stationId, A<CancellationToken>._)).Returns(Task.FromResult<FestivalStation?>(null));

    Result<StationQueueChange, StationQueueFailure> change = await _service.HideFromAsItComesQueueAsync(Guid.NewGuid(), _stationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(change.IsSuccess, Is.False);
                      Assert.That(change.Failure.Reason, Is.EqualTo(StationQueueFailureReason.StationNotAtTheFestival));
                    });

    A.CallTo(() => _stationOrderRepository.FindAtStationAsync(A<Guid>._, A<Guid>._, A<Guid>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  private void GivenItemsAtThisStation(params OrderItem[] items)
  {
    A.CallTo(() => _stationOrderRepository.FindItemsAtStationAsync(A<IReadOnlyCollection<Guid>>._, _stationId, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<OrderItem>>(items.ToList()));
  }

  private OrderItem OpenItem()
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

  private StationOrder AsItComesStationOrder()
  {
    return new()
           {
             Id = Guid.NewGuid(),
             OrderId = _orderId,
             FestivalId = _festivalId,
             StationId = _stationId,
             StationOrderNumber = 1,
             DeliveryMode = DeliveryMode.AsItComes
           };
  }

  private Station Kitchen()
  {
    return new()
           {
             Id = _stationId,
             Name = "Kueche",
             SortOrder = 1,
             IsActive = true
           };
  }

  private Festival RunningFestival()
  {
    return new()
           {
             Id = _festivalId,
             Name = "Sommerfest",
             StartsAtUtc = _now.AddHours(-2),
             EndsAtUtc = _now.AddHours(5),
             NextOrderNumber = 1,
             IsHidden = false
           };
  }

  private FestivalStation Link()
  {
    return new()
           {
             Id = Guid.NewGuid(),
             FestivalId = _festivalId,
             StationId = _stationId,
             NextStationOrderNumber = 1
           };
  }
}
