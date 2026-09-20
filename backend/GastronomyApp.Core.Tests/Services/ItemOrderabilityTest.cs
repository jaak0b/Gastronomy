using FakeItEasy;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class ItemOrderabilityTest
{
  [SetUp]
  public void SetUp()
  {
    _repository = A.Fake<IItemOrderabilityRepository>();
    _festivalRepository = A.Fake<IFestivalRepository>();
    _clock = A.Fake<IClock>();

    A.CallTo(() => _clock.UtcNow).Returns(_now);
    A.CallTo(() => _festivalRepository.FindIdsNotEndedAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<Guid>>([_festivalId]));
    A.CallTo(() => _repository.FindActiveStationIdsAtFestivalAsync(_festivalId, A<CancellationToken>._))
   .Returns(Task.FromResult<IReadOnlyList<Guid>>([
                                                   _kitchenId,
                                                   _barId
                                                 ]));
    A.CallTo(() => _repository.FindActiveMenuItemIdsAsync(_festivalId, A<CancellationToken>._))
   .Returns(Task.FromResult<IReadOnlyList<Guid>>([
                                                   _bratwurstId,
                                                   _beerId
                                                 ]));

    A.CallTo(() => _repository.FindItemIdsPreparedByAsync(A<Guid>._, A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<Guid>>([]));

    GivenStationsPrepare([
                           _kitchenId,
                           _barId
                         ],
                         [
                           _bratwurstId,
                           _beerId
                         ]);
    GivenStationsPrepare([_kitchenId], [_bratwurstId]);
    GivenStationsPrepare([_barId], [_beerId]);

    _orderability = new(_repository, _festivalRepository, _clock);
  }

  private readonly DateTime _now = new(2026, 8, 27, 18, 0, 0, DateTimeKind.Utc);
  private readonly Guid _barId = Guid.Parse("dddddddd-0000-0000-0000-000000000002");
  private readonly Guid _beerId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
  private readonly Guid _bratwurstId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
  private readonly Guid _festivalId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
  private readonly Guid _kitchenId = Guid.Parse("dddddddd-0000-0000-0000-000000000001");

  private IClock _clock = null!;
  private IFestivalRepository _festivalRepository = null!;
  private ItemOrderability _orderability = null!;
  private IItemOrderabilityRepository _repository = null!;

  [Test]
  public void AnyOfTheseStationsPreparesAtAsync_NullStationIds_ThrowsArgumentNullException()
  {
    Assert.That(async () => await _orderability.AnyOfTheseStationsPreparesAtAsync(_festivalId, null!, CancellationToken.None), Throws.ArgumentNullException);
  }

  [Test]
  public void FindItemsStrandedByRemovingStationsAsync_NullStationIds_ThrowsArgumentNullException()
  {
    Assert.That(async () => await _orderability.FindItemsStrandedByRemovingStationsAsync(_festivalId, null!, CancellationToken.None), Throws.ArgumentNullException);
  }

  [Test]
  public async Task FindOrderableItemIdsAsync_EveryItemPreparedAtAnActiveStation_ReturnsThemAll()
  {
    IReadOnlyList<Guid> orderable = await _orderability.FindOrderableItemIdsAsync(_festivalId, CancellationToken.None);

    Assert.That(orderable,
                Is.EqualTo(new[]
                           {
                             _bratwurstId,
                             _beerId
                           }));
  }

  [Test]
  public async Task FindOrderableItemIdsAsync_ItemOnTheMenuThatNoStationPrepares_LeavesItOut()
  {
    A.CallTo(() => _repository.FindActiveMenuItemIdsAsync(_festivalId, A<CancellationToken>._))
   .Returns(Task.FromResult<IReadOnlyList<Guid>>([
                                                   _bratwurstId,
                                                   _beerId,
                                                   Guid.NewGuid()
                                                 ]));

    IReadOnlyList<Guid> orderable = await _orderability.FindOrderableItemIdsAsync(_festivalId, CancellationToken.None);

    Assert.That(orderable,
                Is.EqualTo(new[]
                           {
                             _bratwurstId,
                             _beerId
                           }));
  }

  [Test]
  public async Task AnyOfTheseStationsPreparesAtAsync_AStationThatIsNotAtTheFestival_AnswersFalse()
  {
    var answer = await _orderability.AnyOfTheseStationsPreparesAtAsync(_festivalId, [Guid.NewGuid()], CancellationToken.None);

    Assert.That(answer, Is.False);
  }

  [Test]
  public async Task AnyOfTheseStationsPreparesAtAsync_AnActiveStationOfTheFestival_AnswersTrue()
  {
    var answer = await _orderability.AnyOfTheseStationsPreparesAtAsync(_festivalId, [_kitchenId], CancellationToken.None);

    Assert.That(answer, Is.True);
  }

  [Test]
  public async Task FindItemsStrandedByRemovingStationsAsync_TheOnlyStationOfAnItem_NamesThatItem()
  {
    IReadOnlyList<Guid> stranded = await _orderability.FindItemsStrandedByRemovingStationsAsync(_festivalId, [_kitchenId], CancellationToken.None);

    Assert.That(stranded, Is.EqualTo(new[] { _bratwurstId }));
  }

  [Test]
  public async Task FindItemsStrandedByRemovingStationsAsync_AStationThatSharesTheItem_NamesNothing()
  {
    GivenStationsPrepare([_barId],
                         [
                           _beerId,
                           _bratwurstId
                         ]);

    IReadOnlyList<Guid> stranded = await _orderability.FindItemsStrandedByRemovingStationsAsync(_festivalId, [_kitchenId], CancellationToken.None);

    Assert.That(stranded, Is.Empty);
  }

  [Test]
  public async Task FindItemsStrandedBySwitchingOffStationAsync_AFestivalStillToCome_NamesTheItemsOfThatFestival()
  {
    IReadOnlyList<Guid> stranded = await _orderability.FindItemsStrandedBySwitchingOffStationAsync(_kitchenId, CancellationToken.None);

    Assert.That(stranded, Is.EqualTo(new[] { _bratwurstId }));
  }

  [Test]
  public async Task FindItemsStrandedBySwitchingOffStationAsync_NoFestivalStillToCome_NamesNothing()
  {
    A.CallTo(() => _festivalRepository.FindIdsNotEndedAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<Guid>>([]));

    IReadOnlyList<Guid> stranded = await _orderability.FindItemsStrandedBySwitchingOffStationAsync(_kitchenId, CancellationToken.None);

    Assert.That(stranded, Is.Empty);
  }

  private void GivenStationsPrepare(IReadOnlyList<Guid> stationIds, IReadOnlyList<Guid> itemIds)
  {
    A.CallTo(() => _repository.FindItemIdsPreparedByAsync(_festivalId, A<IReadOnlyCollection<Guid>>.That.Matches(stations => stations.Count == stationIds.Count && stations.All(stationIds.Contains)), A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<Guid>>(itemIds.ToList()));
  }
}
