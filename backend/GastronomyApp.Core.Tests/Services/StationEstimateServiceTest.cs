using FakeItEasy;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class StationEstimateServiceTest
{
  [SetUp]
  public void SetUp()
  {
    _stationRepository = A.Fake<IStationRepository>();
    _stationOrderRepository = A.Fake<IStationOrderRepository>();
    _festivalRepository = A.Fake<IFestivalRepository>();
    _clock = A.Fake<IClock>();

    A.CallTo(() => _clock.UtcNow).Returns(_now);
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._))
     .Returns(Task.FromResult<Festival?>(RunningFestival()));
    A.CallTo(() => _stationRepository.FindAtFestivalAsync(_festivalId, A<CancellationToken>._))
     .Returns(Task.FromResult<IReadOnlyCollection<Station>>([Station(_kitchenId, "Kueche"),
                                                             Station(_barId, "Theke")]));
    A.CallTo(() => _stationOrderRepository.FindQueuedWorkAtFestivalAsync(A<Guid>._, A<CancellationToken>._))
     .Returns(Task.FromResult<IReadOnlyList<StationQueuedWork>>([]));

    _service = new(_stationRepository,
                   _stationOrderRepository,
                   new RunningFestivalLookup(_festivalRepository, new(), _clock),
                   new());
  }

  private readonly DateTime _now = new(2026, 9, 5, 20, 15, 0, DateTimeKind.Utc);
  private readonly Guid _festivalId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
  private readonly Guid _kitchenId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
  private readonly Guid _barId = Guid.Parse("cccccccc-0000-0000-0000-000000000002");

  private IClock _clock = null!;
  private IFestivalRepository _festivalRepository = null!;
  private IStationOrderRepository _stationOrderRepository = null!;
  private IStationRepository _stationRepository = null!;
  private StationEstimateService _service = null!;

  [Test]
  public async Task ReadAsync_NoFestivalIsRunning_ReportsNoStation()
  {
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._))
     .Returns(Task.FromResult<Festival?>(null));

    Assert.That(await _service.ReadAsync(TestContext.CurrentContext.CancellationToken), Is.Empty);
  }

  [Test]
  public async Task ReadAsync_NoOrdersYet_ReportsEveryStationAtTheFestivalAsEmpty()
  {
    IReadOnlyList<StationEstimate> estimates =
      await _service.ReadAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(estimates.Select(estimate => estimate.StationId),
                                  Is.EqualTo(new[] { _kitchenId, _barId }));
                      Assert.That(estimates.Select(estimate => estimate.QueuedMinutes), Is.All.Zero);
                    });
  }

  [Test]
  public async Task ReadAsync_QueuedWorkAtOneStation_SumsOnlyTheMinutesOfThatStation()
  {
    GivenQueuedWork(QueuedWorkAt(_kitchenId, 4, false), QueuedWorkAt(_kitchenId, 4, false));

    IReadOnlyList<StationEstimate> estimates =
      await _service.ReadAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(estimates[0].QueuedMinutes, Is.EqualTo(8));
                      Assert.That(estimates[1].QueuedMinutes, Is.Zero);
                    });
  }

  [Test]
  public async Task ReadAsync_AnArticlePreparedBesideTheQueue_LeavesItOutOfTheSum()
  {
    GivenQueuedWork(QueuedWorkAt(_kitchenId, 4, false), QueuedWorkAt(_kitchenId, 30, true));

    IReadOnlyList<StationEstimate> estimates =
      await _service.ReadAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(estimates[0].QueuedMinutes, Is.EqualTo(4));
  }

  [Test]
  public async Task ReadAsync_AFestivalIsRunning_ReadsTheQueuedWorkOfThatFestival()
  {
    await _service.ReadAsync(TestContext.CurrentContext.CancellationToken);

    A.CallTo(() => _stationOrderRepository.FindQueuedWorkAtFestivalAsync(_festivalId, A<CancellationToken>._))
     .MustHaveHappened();
  }

  private void GivenQueuedWork(params StationQueuedWork[] work)
  {
    A.CallTo(() => _stationOrderRepository.FindQueuedWorkAtFestivalAsync(_festivalId, A<CancellationToken>._))
     .Returns(Task.FromResult<IReadOnlyList<StationQueuedWork>>([.. work]));
  }

  private StationQueuedWork QueuedWorkAt(Guid stationId, double? productionMinutes, bool isQueueIndependent)
  {
    return new()
           {
             StationId = stationId,
             Work = new(productionMinutes, isQueueIndependent)
           };
  }

  private Station Station(Guid stationId, string name)
  {
    return new()
           {
             Id = stationId,
             Name = name,
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
}
