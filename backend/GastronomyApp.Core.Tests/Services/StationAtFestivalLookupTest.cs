using FakeItEasy;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class StationAtFestivalLookupTest
{
  [SetUp]
  public void SetUp()
  {
    _stationRepository = A.Fake<IStationRepository>();
    _festivalRepository = A.Fake<IFestivalRepository>();
    _festivalStationRepository = A.Fake<IFestivalStationRepository>();
    _clock = A.Fake<IClock>();

    A.CallTo(() => _clock.UtcNow).Returns(_now);
    A.CallTo(() => _stationRepository.FindByIdAsync(_stationId, A<CancellationToken>._)).Returns(Task.FromResult<Station?>(Kitchen()));
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(RunningFestival()));
    A.CallTo(() => _festivalStationRepository.FindLinkAsync(_festivalId, _stationId, A<CancellationToken>._)).Returns(Task.FromResult<FestivalStation?>(Link()));

    _lookup = new(_stationRepository, _festivalStationRepository, new(_festivalRepository, new(), _clock));
  }

  private readonly DateTime _now = new(2026, 9, 5, 20, 15, 0, DateTimeKind.Utc);
  private readonly Guid _festivalId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
  private readonly Guid _stationId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

  private IClock _clock = null!;
  private IFestivalRepository _festivalRepository = null!;
  private IFestivalStationRepository _festivalStationRepository = null!;
  private IStationRepository _stationRepository = null!;
  private StationAtFestivalLookup _lookup = null!;

  [Test]
  public async Task FindAsync_TheStationTakesPartInTheRunningFestival_NamesTheStationAndTheFestival()
  {
    Result<StationAtFestival, StationQueueFailure> stationAtFestival = await _lookup.FindAsync(_stationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(stationAtFestival.IsSuccess, Is.True);
                      Assert.That(stationAtFestival.Value.Station.Name, Is.EqualTo("Kueche"));
                      Assert.That(stationAtFestival.Value.FestivalId, Is.EqualTo(_festivalId));
                    });
  }

  [Test]
  public async Task FindAsync_AnUnknownStation_RefusesBecauseTheStationIsUnknown()
  {
    A.CallTo(() => _stationRepository.FindByIdAsync(_stationId, A<CancellationToken>._)).Returns(Task.FromResult<Station?>(null));

    Result<StationAtFestival, StationQueueFailure> stationAtFestival = await _lookup.FindAsync(_stationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(stationAtFestival.IsSuccess, Is.False);
                      Assert.That(stationAtFestival.Failure.Reason, Is.EqualTo(StationQueueFailureReason.StationUnknown));
                    });
  }

  [Test]
  public async Task FindAsync_NoFestivalIsRunning_RefusesBecauseNoFestivalIsRunning()
  {
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(null));

    Result<StationAtFestival, StationQueueFailure> stationAtFestival = await _lookup.FindAsync(_stationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(stationAtFestival.IsSuccess, Is.False);
                      Assert.That(stationAtFestival.Failure.Reason, Is.EqualTo(StationQueueFailureReason.NoRunningFestival));
                    });
  }

  [Test]
  public async Task FindAsync_TheStationIsNotAtTheRunningFestival_RefusesBecauseItDoesNotTakePart()
  {
    A.CallTo(() => _festivalStationRepository.FindLinkAsync(_festivalId, _stationId, A<CancellationToken>._)).Returns(Task.FromResult<FestivalStation?>(null));

    Result<StationAtFestival, StationQueueFailure> stationAtFestival = await _lookup.FindAsync(_stationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(stationAtFestival.IsSuccess, Is.False);
                      Assert.That(stationAtFestival.Failure.Reason, Is.EqualTo(StationQueueFailureReason.StationNotAtTheFestival));
                    });
  }

  [Test]
  public async Task FindAsync_AFestivalIsRunning_AsksForTheFestivalThatIsRunningNow()
  {
    await _lookup.FindAsync(_stationId, TestContext.CurrentContext.CancellationToken);

    A.CallTo(() => _festivalRepository.FindRunningAsync(_now, A<CancellationToken>._)).MustHaveHappened();
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
