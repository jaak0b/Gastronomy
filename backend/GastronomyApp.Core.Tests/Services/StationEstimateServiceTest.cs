using FakeItEasy;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Services;
using Microsoft.Extensions.Time.Testing;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class StationEstimateServiceTest
{
  [SetUp]
  public void SetUp()
  {
    _stationRepository = A.Fake<IStationRepository>();
    _festivalRepository = A.Fake<IFestivalRepository>();
    _clock = new FakeTimeProvider(new(_now));

    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(RunningFestival()));
    A.CallTo(() => _stationRepository.FindAtFestivalWithOpenItemsAsync(_festivalId, A<CancellationToken>._))
   .Returns(Task.FromResult<IReadOnlyList<Station>>([
                                                      Station(_kitchenId, "Kueche"),
                                                      Station(_barId, "Theke")
                                                    ]));

    _service = new(_stationRepository, new(_festivalRepository, new(), _clock));
  }

  private readonly DateTime _now = new(2026, 9, 5, 20, 15, 0, DateTimeKind.Utc);
  private readonly Guid _festivalId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
  private readonly Guid _kitchenId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
  private readonly Guid _barId = Guid.Parse("cccccccc-0000-0000-0000-000000000002");

  private TimeProvider _clock = null!;
  private IFestivalRepository _festivalRepository = null!;
  private IStationRepository _stationRepository = null!;
  private StationEstimateService _service = null!;

  [Test]
  public async Task ReadStationsWithOpenWorkAsync_NoFestivalIsRunning_ReportsNoStation()
  {
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(null));

    Assert.That(await _service.ReadStationsWithOpenWorkAsync(TestContext.CurrentContext.CancellationToken), Is.Empty);
  }

  [Test]
  public async Task ReadStationsWithOpenWorkAsync_AFestivalIsRunning_ReportsEveryStationOfThatFestival()
  {
    IReadOnlyList<Station> stations = await _service.ReadStationsWithOpenWorkAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(stations.Select(station => station.Id),
                Is.EqualTo(new[]
                           {
                             _kitchenId,
                             _barId
                           }));
  }

  [Test]
  public async Task ReadStationsWithOpenWorkAsync_AFestivalIsRunning_ReadsTheOpenWorkOfThatFestival()
  {
    await _service.ReadStationsWithOpenWorkAsync(TestContext.CurrentContext.CancellationToken);

    A.CallTo(() => _stationRepository.FindAtFestivalWithOpenItemsAsync(_festivalId, A<CancellationToken>._)).MustHaveHappened();
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
