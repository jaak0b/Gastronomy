using FakeItEasy;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Services;
using Microsoft.Extensions.Time.Testing;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class ItemsToHandOutLookupTest
{
  [SetUp]
  public void SetUp()
  {
    _stationRepository = A.Fake<IStationRepository>();
    _festivalRepository = A.Fake<IFestivalRepository>();

    A.CallTo(() => _stationRepository.FindStationIdsWithItemsToHandOutAsync(_festivalId, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyCollection<Guid>>([_kitchenId]));
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(null));

    _lookup = new(_stationRepository, new(_festivalRepository, new(), new FakeTimeProvider(new(_now))));
  }

  private readonly DateTime _now = new(2026, 8, 27, 18, 0, 0, DateTimeKind.Utc);
  private readonly Guid _festivalId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
  private readonly Guid _kitchenId = Guid.Parse("dddddddd-0000-0000-0000-000000000001");

  private IFestivalRepository _festivalRepository = null!;
  private ItemsToHandOutLookup _lookup = null!;
  private IStationRepository _stationRepository = null!;

  [Test]
  public async Task FindStationIdsIfRunningAsync_AFestivalThatRuns_NamesTheStationsWithItemsToHandOut()
  {
    IReadOnlyCollection<Guid> stationIds = await _lookup.FindStationIdsIfRunningAsync(BuildFestival(_now.AddHours(-1), _now.AddHours(5)), CancellationToken.None);

    Assert.That(stationIds, Is.EqualTo(new[] { _kitchenId }));
  }

  [Test]
  public async Task FindStationIdsIfRunningAsync_AFestivalThatIsOver_NamesNoStationWithoutAskingForItems()
  {
    IReadOnlyCollection<Guid> stationIds = await _lookup.FindStationIdsIfRunningAsync(BuildFestival(_now.AddHours(-6), _now.AddHours(-1)), CancellationToken.None);

    Assert.That(stationIds, Is.Empty);

    A.CallTo(() => _stationRepository.FindStationIdsWithItemsToHandOutAsync(A<Guid>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task FindStationIdsIfRunningAsync_AFestivalThatHasNotStarted_NamesNoStation()
  {
    IReadOnlyCollection<Guid> stationIds = await _lookup.FindStationIdsIfRunningAsync(BuildFestival(_now.AddHours(1), _now.AddHours(6)), CancellationToken.None);

    Assert.That(stationIds, Is.Empty);
  }

  [Test]
  public async Task FindStationIdsAtTheRunningFestivalAsync_AFestivalRuns_NamesItsStationsWithItemsToHandOut()
  {
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(BuildFestival(_now.AddHours(-1), _now.AddHours(5))));

    IReadOnlyCollection<Guid> stationIds = await _lookup.FindStationIdsAtTheRunningFestivalAsync(CancellationToken.None);

    Assert.That(stationIds, Is.EqualTo(new[] { _kitchenId }));
  }

  [Test]
  public async Task FindStationIdsAtTheRunningFestivalAsync_NoFestivalRuns_NamesNoStation()
  {
    IReadOnlyCollection<Guid> stationIds = await _lookup.FindStationIdsAtTheRunningFestivalAsync(CancellationToken.None);

    Assert.That(stationIds, Is.Empty);
  }

  [Test]
  public void FindStationIdsIfRunningAsync_NoFestival_ThrowsArgumentNullException()
  {
    Assert.That(async () => await _lookup.FindStationIdsIfRunningAsync(null!, CancellationToken.None), Throws.ArgumentNullException);
  }

  private Festival BuildFestival(DateTime startsAtUtc, DateTime endsAtUtc)
  {
    return new()
           {
             Id = _festivalId,
             Name = "Sommerfest",
             StartsAtUtc = startsAtUtc,
             EndsAtUtc = endsAtUtc,
             NextOrderNumber = 1,
             IsHidden = false
           };
  }
}
