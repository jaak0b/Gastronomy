using ErrorOr;
using FakeItEasy;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Services;
using Microsoft.Extensions.Time.Testing;
using GastronomyApp.Core.Tests.TestSupport;

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
    _clock = new FakeTimeProvider(new(_now));

    A.CallTo(() => _stationRepository.FindByIdAsync(_stationId, A<CancellationToken>._)).Returns(Task.FromResult<Station?>(Kitchen()));
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(RunningFestival()));
    A.CallTo(() => _festivalStationRepository.FindLinkAsync(_festivalId, _stationId, A<CancellationToken>._)).Returns(Task.FromResult<FestivalStation?>(Link()));
    A.CallTo(() => _repository.FindStationWithUnfinishedOrdersAsync(A<Guid>._, A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<Station?>(Kitchen()));
    A.CallTo(() => _repository.FindFulfilledAtStationAsync(A<Guid>._, A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<StationOrder>>([]));

    StationAtFestivalLookup lookup = new(_stationRepository, _festivalStationRepository, new(_festivalRepository, new(), _clock));

    _service = new(lookup, _repository);
  }

  private readonly DateTime _now = new(2026, 9, 5, 20, 15, 0, DateTimeKind.Utc);
  private readonly Guid _festivalId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
  private readonly Guid _stationId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

  private TimeProvider _clock = null!;
  private IFestivalRepository _festivalRepository = null!;
  private IFestivalStationRepository _festivalStationRepository = null!;
  private IStationOrderRepository _repository = null!;
  private IStationRepository _stationRepository = null!;
  private StationQueueService _service = null!;

  [Test]
  public async Task ReadQueueAsync_TheStationHasOpenOrders_NamesTheStationAndCarriesThem()
  {
    GivenUnfinished(StationOrder(1, DeliveryMode.Together), StationOrder(2, DeliveryMode.AsItComes));

    ErrorOr<Station> queue = await _service.ReadQueueAsync(_stationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(queue.IsSuccess, Is.True);
                      Assert.That(queue.Value.Id, Is.EqualTo(_stationId));
                      Assert.That(queue.Value.Name, Is.EqualTo("Kueche"));
                      Assert.That(queue.Value.StationOrders.Select(stationOrder => stationOrder.StationOrderNumber),
                                  Is.EqualTo(new[]
                                             {
                                               1,
                                               2
                                             }));
                    });
  }

  [Test]
  public async Task ReadQueueAsync_NoFestivalIsRunning_RefusesWithTheReasonOfTheLookup()
  {
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(null));

    ErrorOr<Station> queue = await _service.ReadQueueAsync(_stationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(queue.IsSuccess, Is.False);
                      Assert.That(queue.RefusalMessageKey(), Is.EqualTo("station.noFestivalIsRunning"));
                    });
  }

  [Test]
  public async Task ReadQueueAsync_TheStationIsGoneWhileTheQueueIsRead_RefusesBecauseTheStationIsUnknown()
  {
    A.CallTo(() => _repository.FindStationWithUnfinishedOrdersAsync(A<Guid>._, A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<Station?>(null));

    ErrorOr<Station> queue = await _service.ReadQueueAsync(_stationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(queue.IsSuccess, Is.False);
                      Assert.That(queue.RefusalMessageKey(), Is.EqualTo("StationUnknown"));
                    });
  }

  [Test]
  public async Task ReadQueueAsync_AStationThatTakesPart_ReadsTheQueueOfThatStationAtThatFestival()
  {
    await _service.ReadQueueAsync(_stationId, TestContext.CurrentContext.CancellationToken);

    A.CallTo(() => _repository.FindStationWithUnfinishedOrdersAsync(_festivalId, _stationId, A<CancellationToken>._)).MustHaveHappened();
  }

  [Test]
  public void ReadQueueAtAsync_NoStation_ThrowsArgumentNullException()
  {
    Assert.That(async () => await _service.ReadQueueAtAsync(null!, TestContext.CurrentContext.CancellationToken), Throws.ArgumentNullException);
  }

  [Test]
  public async Task ReadFulfilledAsync_TheStationHandedItemsOut_ListsThoseStationOrders()
  {
    A.CallTo(() => _repository.FindFulfilledAtStationAsync(_festivalId, _stationId, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<StationOrder>>([StationOrder(1, DeliveryMode.Together)]));

    ErrorOr<IReadOnlyList<StationOrder>> fulfilled = await _service.ReadFulfilledAsync(_stationId, TestContext.CurrentContext.CancellationToken);

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

    ErrorOr<IReadOnlyList<StationOrder>> fulfilled = await _service.ReadFulfilledAsync(_stationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(fulfilled.IsSuccess, Is.False);
                      Assert.That(fulfilled.RefusalMessageKey(), Is.EqualTo("station.notPartOfTheFestival"));
                    });
  }

  private void GivenUnfinished(params StationOrder[] stationOrders)
  {
    var station = Kitchen();

    foreach (var stationOrder in stationOrders)
      station.StationOrders.Add(stationOrder);

    A.CallTo(() => _repository.FindStationWithUnfinishedOrdersAsync(_festivalId, _stationId, A<CancellationToken>._)).Returns(Task.FromResult<Station?>(station));
  }

  private StationOrder StationOrder(int stationOrderNumber, DeliveryMode deliveryMode)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             OrderId = Guid.NewGuid(),
             FestivalId = _festivalId,
             StationId = _stationId,
             StationOrderNumber = stationOrderNumber,
             DeliveryMode = deliveryMode
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
