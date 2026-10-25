using FakeItEasy;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;
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
    _tablePlanRepository = A.Fake<ITablePlanRepository>();
    _festivalRepository = A.Fake<IFestivalRepository>();

    A.CallTo(() => _stationRepository.FindStationIdsWithItemsToHandOutAsync(_festivalId, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyCollection<Guid>>([_kitchenId]));
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(null));

    A.CallTo(() => _tablePlanRepository.HasItemsToHandOutOrSettleInAreaAsync(_gardenId, A<CancellationToken>._)).Returns(true);
    A.CallTo(() => _tablePlanRepository.HasItemsToHandOutOrSettleAtTableAsync(_tableId, A<CancellationToken>._)).Returns(true);

    _lookup = new(_stationRepository, _tablePlanRepository, new(_festivalRepository, new(), new FakeTimeProvider(new(_now))));
  }

  private readonly DateTime _now = new(2026, 8, 27, 18, 0, 0, DateTimeKind.Utc);
  private readonly Guid _festivalId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
  private readonly Guid _gardenId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
  private readonly Guid _kitchenId = Guid.Parse("dddddddd-0000-0000-0000-000000000001");
  private readonly Guid _tableId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

  private IFestivalRepository _festivalRepository = null!;
  private ItemsToHandOutLookup _lookup = null!;
  private IStationRepository _stationRepository = null!;
  private ITablePlanRepository _tablePlanRepository = null!;

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

  [Test]
  public async Task FindTablePlanAsync_AFestivalThatRuns_AsksForTheTablePlanCountingTheItemsLeft()
  {
    IReadOnlyList<ListedArea> counted = [];
    A.CallTo(() => _tablePlanRepository.FindTablePlanAsync(_festivalId, true, A<CancellationToken>._)).Returns(Task.FromResult(counted));

    IReadOnlyList<ListedArea> tablePlan = await _lookup.FindTablePlanAsync(BuildFestival(_now.AddHours(-1), _now.AddHours(5)), CancellationToken.None);

    Assert.That(tablePlan, Is.SameAs(counted));
  }

  [TestCase(-6, -1)]
  [TestCase(1, 6)]
  public async Task FindTablePlanAsync_AFestivalThatDoesNotRun_AsksForTheTablePlanWithoutCountingTheItemsLeft(int startsInHours, int endsInHours)
  {
    IReadOnlyList<ListedArea> uncounted = [];
    A.CallTo(() => _tablePlanRepository.FindTablePlanAsync(_festivalId, false, A<CancellationToken>._)).Returns(Task.FromResult(uncounted));

    IReadOnlyList<ListedArea> tablePlan = await _lookup.FindTablePlanAsync(BuildFestival(_now.AddHours(startsInHours), _now.AddHours(endsInHours)), CancellationToken.None);

    Assert.That(tablePlan, Is.SameAs(uncounted));
  }

  [TestCase(-1, 5, true)]
  [TestCase(-6, -1, false)]
  [TestCase(1, 6, false)]
  public async Task HasItemsToHandOutOrSettleInAreaIfRunningAsync_AnAreaWithItemsLeft_CountsThemOnlyWhileTheFestivalRuns(int startsInHours, int endsInHours, bool expected)
  {
    var hasItemsLeft = await _lookup.HasItemsToHandOutOrSettleInAreaIfRunningAsync(BuildFestival(_now.AddHours(startsInHours), _now.AddHours(endsInHours)), _gardenId, CancellationToken.None);

    Assert.That(hasItemsLeft, Is.EqualTo(expected));
  }

  [TestCase(-1, 5, true)]
  [TestCase(-6, -1, false)]
  [TestCase(1, 6, false)]
  public async Task HasItemsToHandOutOrSettleAtTableIfRunningAsync_ATableWithItemsLeft_CountsThemOnlyWhileTheFestivalRuns(int startsInHours, int endsInHours, bool expected)
  {
    var hasItemsLeft = await _lookup.HasItemsToHandOutOrSettleAtTableIfRunningAsync(BuildFestival(_now.AddHours(startsInHours), _now.AddHours(endsInHours)), _tableId, CancellationToken.None);

    Assert.That(hasItemsLeft, Is.EqualTo(expected));
  }

  [Test]
  public void FindTablePlanAsync_NoFestival_ThrowsArgumentNullException()
  {
    Assert.That(async () => await _lookup.FindTablePlanAsync(null!, CancellationToken.None), Throws.ArgumentNullException);
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
