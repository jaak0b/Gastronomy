using FakeItEasy;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using GastronomyApp.Core.Tests.TestSupport;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class FestivalStationServiceTest
{
  [SetUp]
  public void SetUp()
  {
    _repository = A.Fake<IFestivalStationRepository>();
    _festivalRepository = A.Fake<IFestivalRepository>();
    _stationRepository = A.Fake<IStationRepository>();
    _orderabilityRepository = A.Fake<IItemOrderabilityRepository>();
    _numberAllocator = A.Fake<INumberAllocator>();
    _clock = A.Fake<IClock>();
    _transactionRunner = new();

    A.CallTo(() => _clock.UtcNow).Returns(_now);
    A.CallTo(() => _festivalRepository.ExistsAsync(A<Guid>._, A<CancellationToken>._)).Returns(true);
    A.CallTo(() => _festivalRepository.FindByIdAsync(A<Guid>._, A<CancellationToken>._))
     .Returns(Task.FromResult<Festival?>(BuildFestival(false)));
    A.CallTo(() => _stationRepository.ExistsAsync(A<Guid>._, A<CancellationToken>._)).Returns(true);
    A.CallTo(() => _repository.FindLinkAsync(A<Guid>._, A<Guid>._, A<CancellationToken>._))
     .Returns(Task.FromResult<FestivalStation?>(null));
    A.CallTo(() => _repository.FindAssignmentsAtStationAsync(A<Guid>._, A<Guid>._, A<CancellationToken>._))
     .Returns(Task.FromResult<IReadOnlyList<ItemStationAssignment>>([]));
    A.CallTo(() => _repository.CountUnfulfilledItemsAsync(A<Guid>._, A<Guid>._, A<CancellationToken>._)).Returns(0);
    A.CallTo(() => _numberAllocator.FindNextStationOrderNumberAsync(A<Guid>._, A<Guid>._, A<CancellationToken>._))
     .Returns(7);
    A.CallTo(() => _orderabilityRepository.FindActiveStationIdsAtFestivalAsync(A<Guid>._, A<CancellationToken>._))
     .Returns(Task.FromResult<IReadOnlyList<Guid>>([_kitchenId]));
    A.CallTo(() => _orderabilityRepository.FindItemIdsPreparedByAsync(A<Guid>._,
                                                                      A<IReadOnlyCollection<Guid>>._,
                                                                      A<CancellationToken>._))
     .Returns(Task.FromResult<IReadOnlyList<Guid>>([]));
    A.CallTo(() => _orderabilityRepository.FindActiveMenuItemIdsAsync(A<Guid>._, A<CancellationToken>._))
     .Returns(Task.FromResult<IReadOnlyList<Guid>>([]));

    _service = new(_repository,
                   _festivalRepository,
                   _stationRepository,
                   new(_orderabilityRepository, _festivalRepository, _clock),
                   _numberAllocator,
                   new RunningFestivalLookup(_festivalRepository, new(), _clock),
                   _transactionRunner);
  }

  private readonly DateTime _now = new(2026, 8, 27, 18, 0, 0, DateTimeKind.Utc);
  private readonly Guid _bratwurstId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
  private readonly Guid _festivalId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
  private readonly Guid _kitchenId = Guid.Parse("dddddddd-0000-0000-0000-000000000001");

  private IClock _clock = null!;
  private IFestivalRepository _festivalRepository = null!;
  private INumberAllocator _numberAllocator = null!;
  private IItemOrderabilityRepository _orderabilityRepository = null!;
  private IFestivalStationRepository _repository = null!;
  private FestivalStationService _service = null!;
  private IStationRepository _stationRepository = null!;
  private RecordingTransactionRunner _transactionRunner = null!;

  [Test]
  public async Task AddAsync_AStationThatIsNotThere_FailsBecauseTheStationIsNotFound()
  {
    A.CallTo(() => _stationRepository.ExistsAsync(_kitchenId, A<CancellationToken>._)).Returns(false);

    Result<SavedFestivalStation, FestivalStationFailure> added =
      await _service.AddAsync(_festivalId, _kitchenId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(added.IsSuccess, Is.False);
                      Assert.That(added.Failure.Reason, Is.EqualTo(FestivalStationFailureReason.StationNotFound));
                    });
  }

  [Test]
  public async Task AddAsync_AStationAlreadyAtTheFestival_ChangesNothingAndRollsBack()
  {
    A.CallTo(() => _repository.FindLinkAsync(_festivalId, _kitchenId, A<CancellationToken>._))
     .Returns(Task.FromResult<FestivalStation?>(BuildLink()));

    Result<SavedFestivalStation, FestivalStationFailure> added =
      await _service.AddAsync(_festivalId, _kitchenId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(added.IsSuccess, Is.True);
                      Assert.That(added.Value.SomethingChanged, Is.False);
                      Assert.That(_transactionRunner.Committed, Is.False);
                    });
  }

  [Test]
  public async Task AddAsync_AStationComingBackToTheFestival_ContinuesTheNumberingWhereItStopped()
  {
    Result<SavedFestivalStation, FestivalStationFailure> added =
      await _service.AddAsync(_festivalId, _kitchenId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(added.IsSuccess, Is.True);
                      Assert.That(_transactionRunner.Committed, Is.True);
                    });

    A.CallTo(() => _repository.AddLinkAsync(A<FestivalStation>.That.Matches(link => link.NextStationOrderNumber == 7),
                                            A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task RemoveAsync_AStationThatIsNotAtTheFestival_FailsBecauseTheLinkIsNotFound()
  {
    Result<SavedFestivalStation, FestivalStationFailure> removed =
      await _service.RemoveAsync(_festivalId, _kitchenId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(removed.IsSuccess, Is.False);
                      Assert.That(removed.Failure.Reason,
                                  Is.EqualTo(FestivalStationFailureReason.StationLinkNotFound));
                    });
  }

  [Test]
  public async Task RemoveAsync_AStationWithOpenItemsAtTheRunningFestival_KeepsTheStationAtTheFestival()
  {
    A.CallTo(() => _repository.FindLinkAsync(_festivalId, _kitchenId, A<CancellationToken>._))
     .Returns(Task.FromResult<FestivalStation?>(BuildLink()));
    A.CallTo(() => _repository.CountUnfulfilledItemsAsync(_festivalId, _kitchenId, A<CancellationToken>._))
     .Returns(3);

    Result<SavedFestivalStation, FestivalStationFailure> removed =
      await _service.RemoveAsync(_festivalId, _kitchenId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(removed.IsSuccess, Is.False);
                      Assert.That(removed.Failure.Reason,
                                  Is.EqualTo(FestivalStationFailureReason.StationHasUnfulfilledItems));
                      Assert.That(_transactionRunner.Committed, Is.False);
                    });
  }

  [Test]
  public async Task RemoveAsync_AStationWithOpenItemsAtAFestivalThatIsOver_RemovesItAnyway()
  {
    A.CallTo(() => _festivalRepository.FindByIdAsync(_festivalId, A<CancellationToken>._))
     .Returns(Task.FromResult<Festival?>(BuildFestival(true)));
    FestivalStation link = BuildLink();
    A.CallTo(() => _repository.FindLinkAsync(_festivalId, _kitchenId, A<CancellationToken>._))
     .Returns(Task.FromResult<FestivalStation?>(link));
    A.CallTo(() => _repository.CountUnfulfilledItemsAsync(_festivalId, _kitchenId, A<CancellationToken>._))
     .Returns(3);

    Result<SavedFestivalStation, FestivalStationFailure> removed =
      await _service.RemoveAsync(_festivalId, _kitchenId, CancellationToken.None);

    Assert.That(removed.IsSuccess, Is.True);

    A.CallTo(() => _repository.RemoveLink(link)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task RemoveAsync_AStationTheLastItemNeeds_CountsTheItemsThatWouldBeStranded()
  {
    A.CallTo(() => _repository.FindLinkAsync(_festivalId, _kitchenId, A<CancellationToken>._))
     .Returns(Task.FromResult<FestivalStation?>(BuildLink()));
    A.CallTo(() => _orderabilityRepository.FindItemIdsPreparedByAsync(_festivalId,
                                                                      A<IReadOnlyCollection<Guid>>.That.Matches(stationIds =>
                                                                                                                   stationIds.Contains(_kitchenId)),
                                                                      A<CancellationToken>._))
     .Returns(Task.FromResult<IReadOnlyList<Guid>>([_bratwurstId]));
    A.CallTo(() => _orderabilityRepository.FindActiveMenuItemIdsAsync(_festivalId, A<CancellationToken>._))
     .Returns(Task.FromResult<IReadOnlyList<Guid>>([_bratwurstId]));

    Result<SavedFestivalStation, FestivalStationFailure> removed =
      await _service.RemoveAsync(_festivalId, _kitchenId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(removed.IsSuccess, Is.False);
                      Assert.That(removed.Failure.Reason,
                                  Is.EqualTo(FestivalStationFailureReason.ItemsWouldHaveNoStation));
                      Assert.That(removed.Failure.StrandedItemCount, Is.EqualTo(1));
                    });
  }

  private FestivalStation BuildLink()
  {
    return new()
           {
             Id = Guid.NewGuid(),
             FestivalId = _festivalId,
             StationId = _kitchenId,
             NextStationOrderNumber = 1
           };
  }

  private Festival BuildFestival(bool hasEnded)
  {
    return new()
           {
             Id = _festivalId,
             Name = "Sommerfest",
             StartsAtUtc = _now.AddHours(-5),
             EndsAtUtc = hasEnded ? _now.AddHours(-1) : _now.AddHours(5),
             NextOrderNumber = 1,
             IsHidden = false
           };
  }
}
