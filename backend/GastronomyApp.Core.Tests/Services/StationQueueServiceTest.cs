using FakeItEasy;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class StationQueueServiceTest
{
  [SetUp]
  public void SetUp()
  {
    _repository = A.Fake<IStationOrderRepository>();
    _stationRepository = A.Fake<IStationRepository>();
    _festivalRepository = A.Fake<IFestivalRepository>();
    _festivalStationRepository = A.Fake<IFestivalStationRepository>();
    _clock = A.Fake<IClock>();

    A.CallTo(() => _clock.UtcNow).Returns(_now);
    A.CallTo(() => _stationRepository.FindByIdAsync(_stationId, A<CancellationToken>._)).Returns(Task.FromResult<Station?>(Kitchen()));
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(RunningFestival()));
    A.CallTo(() => _festivalStationRepository.FindLinkAsync(_festivalId, _stationId, A<CancellationToken>._)).Returns(Task.FromResult<FestivalStation?>(Link()));
    A.CallTo(() => _repository.FindUnfinishedAtStationAsync(A<Guid>._, A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<QueuedStationOrder>>([]));
    A.CallTo(() => _repository.FindFulfilledAtStationAsync(A<Guid>._, A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<QueuedStationOrder>>([]));

    StationAtFestivalLookup lookup = new(_stationRepository, _festivalStationRepository, new(_festivalRepository, new(), _clock));

    _service = new(lookup, _repository);
  }

  private readonly DateTime _now = new(2026, 9, 5, 20, 15, 0, DateTimeKind.Utc);
  private readonly Guid _festivalId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
  private readonly Guid _stationId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

  private IClock _clock = null!;
  private IFestivalRepository _festivalRepository = null!;
  private IFestivalStationRepository _festivalStationRepository = null!;
  private IStationOrderRepository _repository = null!;
  private IStationRepository _stationRepository = null!;
  private StationQueueService _service = null!;

  [Test]
  public async Task ReadQueueAsync_TheStationHasOpenOrders_NamesTheStationAndListsThem()
  {
    GivenUnfinished(StationOrder(1, DeliveryMode.Together, false), StationOrder(2, DeliveryMode.AsItComes, false));

    Result<StationQueue, StationQueueFailure> queue = await _service.ReadQueueAsync(_stationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(queue.IsSuccess, Is.True);
                      Assert.That(queue.Value.StationId, Is.EqualTo(_stationId));
                      Assert.That(queue.Value.StationName, Is.EqualTo("Kueche"));
                      Assert.That(queue.Value.Orders.Select(stationOrder => stationOrder.StationOrderNumber),
                                  Is.EqualTo(new[]
                                             {
                                               1,
                                               2
                                             }));
                    });
  }

  [Test]
  public async Task ReadQueueAsync_AnAsItComesOrder_AlsoStandsInTheSecondColumn()
  {
    GivenUnfinished(StationOrder(1, DeliveryMode.Together, false), StationOrder(2, DeliveryMode.AsItComes, false));

    Result<StationQueue, StationQueueFailure> queue = await _service.ReadQueueAsync(_stationId, TestContext.CurrentContext.CancellationToken);

    Assert.That(queue.Value.AsItComesOrders.Select(stationOrder => stationOrder.StationOrderNumber), Is.EqualTo(new[] { 2 }));
  }

  [Test]
  public async Task ReadQueueAsync_AnAsItComesOrderTheEmployeeHid_LeavesItOutOfTheSecondColumnOnly()
  {
    GivenUnfinished(StationOrder(1, DeliveryMode.AsItComes, true));

    Result<StationQueue, StationQueueFailure> queue = await _service.ReadQueueAsync(_stationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(queue.Value.Orders, Has.Count.EqualTo(1));
                      Assert.That(queue.Value.AsItComesOrders, Is.Empty);
                    });
  }

  [Test]
  public async Task ReadQueueAsync_NoFestivalIsRunning_RefusesWithTheReasonOfTheLookup()
  {
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(null));

    Result<StationQueue, StationQueueFailure> queue = await _service.ReadQueueAsync(_stationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(queue.IsSuccess, Is.False);
                      Assert.That(queue.Failure.Reason, Is.EqualTo(StationQueueFailureReason.NoRunningFestival));
                    });
  }

  [Test]
  public async Task ReadQueueAsync_AStationThatTakesPart_ReadsTheQueueOfThatStationAtThatFestival()
  {
    await _service.ReadQueueAsync(_stationId, TestContext.CurrentContext.CancellationToken);

    A.CallTo(() => _repository.FindUnfinishedAtStationAsync(_festivalId, _stationId, A<CancellationToken>._)).MustHaveHappened();
  }

  [Test]
  public void ReadQueueAtAsync_NoStation_ThrowsArgumentNullException()
  {
    Assert.That(async () => await _service.ReadQueueAtAsync(null!, TestContext.CurrentContext.CancellationToken), Throws.ArgumentNullException);
  }

  [Test]
  public async Task ReadFulfilledAsync_TheStationHandedItemsOut_ListsThoseStationOrders()
  {
    A.CallTo(() => _repository.FindFulfilledAtStationAsync(_festivalId, _stationId, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<QueuedStationOrder>>([StationOrder(1, DeliveryMode.Together, false)]));

    Result<IReadOnlyList<QueuedStationOrder>, StationQueueFailure> fulfilled = await _service.ReadFulfilledAsync(_stationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(fulfilled.IsSuccess, Is.True);
                      Assert.That(fulfilled.Value, Has.Count.EqualTo(1));
                    });
  }

  [Test]
  public async Task ReadFulfilledAsync_TheStationIsNotAtTheRunningFestival_RefusesWithTheReasonOfTheStanding()
  {
    A.CallTo(() => _festivalStationRepository.FindLinkAsync(_festivalId, _stationId, A<CancellationToken>._)).Returns(Task.FromResult<FestivalStation?>(null));

    Result<IReadOnlyList<QueuedStationOrder>, StationQueueFailure> fulfilled = await _service.ReadFulfilledAsync(_stationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(fulfilled.IsSuccess, Is.False);
                      Assert.That(fulfilled.Failure.Reason, Is.EqualTo(StationQueueFailureReason.StationNotAtTheFestival));
                    });
  }

  private void GivenUnfinished(params QueuedStationOrder[] stationOrders)
  {
    A.CallTo(() => _repository.FindUnfinishedAtStationAsync(_festivalId, _stationId, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<QueuedStationOrder>>(stationOrders.ToList()));
  }

  private QueuedStationOrder StationOrder(int stationOrderNumber, DeliveryMode deliveryMode, bool isHidden)
  {
    return new()
           {
             StationOrderId = Guid.NewGuid(),
             GlobalOrderNumber = stationOrderNumber,
             StationOrderNumber = stationOrderNumber,
             TableName = "Tisch 3",
             StaffMemberName = "Anna",
             DeliveryMode = deliveryMode,
             CreatedAtUtc = _now.AddMinutes(-5),
             IsHiddenFromAsItComesQueue = isHidden,
             ItemCount = 1,
             FulfilledItemCount = 0,
             Items = []
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
