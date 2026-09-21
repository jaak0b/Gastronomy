using FakeItEasy;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using GastronomyApp.Core.Tests.TestSupport;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class FestivalMenuServiceTest
{
  [SetUp]
  public void SetUp()
  {
    _repository = A.Fake<IFestivalMenuRepository>();
    _festivalRepository = A.Fake<IFestivalRepository>();
    _orderabilityRepository = A.Fake<IItemOrderabilityRepository>();
    _clock = A.Fake<IClock>();
    _transactionRunner = new();

    A.CallTo(() => _clock.UtcNow).Returns(_now);
    A.CallTo(() => _festivalRepository.ExistsAsync(A<Guid>._, A<CancellationToken>._)).Returns(true);
    A.CallTo(() => _festivalRepository.FindByIdAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(BuildFestival(false)));
    A.CallTo(() => _repository.CatalogItemExistsAsync(A<Guid>._, A<CancellationToken>._)).Returns(true);
    A.CallTo(() => _repository.FindStationIdsAtFestivalAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<Guid>>([_kitchenId]));
    A.CallTo(() => _repository.FindAssignmentsAsync(A<Guid>._, A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<ItemStationAssignment>>([]));
    A.CallTo(() => _repository.FindMenuRowAsync(A<Guid>._, A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<FestivalCatalogItem?>(null));
    A.CallTo(() => _orderabilityRepository.FindActiveStationIdsAtFestivalAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<Guid>>([_kitchenId]));

    _service = new(_repository, _festivalRepository, new(_orderabilityRepository, _festivalRepository, _clock), new(_festivalRepository, new(), _clock), _transactionRunner);
  }

  private readonly DateTime _now = new(2026, 8, 27, 18, 0, 0, DateTimeKind.Utc);
  private readonly Guid _bratwurstId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
  private readonly Guid _festivalId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
  private readonly Guid _kitchenId = Guid.Parse("dddddddd-0000-0000-0000-000000000001");
  private readonly Guid _strangerStationId = Guid.Parse("dddddddd-0000-0000-0000-000000000009");

  private IClock _clock = null!;
  private IFestivalRepository _festivalRepository = null!;
  private IItemOrderabilityRepository _orderabilityRepository = null!;
  private IFestivalMenuRepository _repository = null!;
  private FestivalMenuService _service = null!;
  private RecordingTransactionRunner _transactionRunner = null!;

  [Test]
  public async Task PutOnTheMenuAsync_APriceAboveTheHighestTheFormAccepts_FailsBecauseThePriceIsOutOfRange()
  {
    Result<FestivalCatalogItem, FestivalMenuFailure> putOn = await _service.PutOnTheMenuAsync(_festivalId, _bratwurstId, 100000, [_kitchenId], CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(putOn.IsSuccess, Is.False);
                      Assert.That(putOn.Failure.Reason, Is.EqualTo(FestivalMenuFailureReason.PriceOutOfRange));
                      Assert.That(_transactionRunner.Committed, Is.False);
                    });
  }

  [Test]
  public async Task PutOnTheMenuAsync_AStationThatIsNotAtTheFestival_NamesTheStationItRefused()
  {
    Result<FestivalCatalogItem, FestivalMenuFailure> putOn = await _service.PutOnTheMenuAsync(_festivalId, _bratwurstId, 350, [_strangerStationId], CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(putOn.IsSuccess, Is.False);
                      Assert.That(putOn.Failure.Reason, Is.EqualTo(FestivalMenuFailureReason.StationsDoNotBelongToTheFestival));
                      Assert.That(putOn.Failure.StationIdsOutsideTheFestival, Is.EqualTo(new[] { _strangerStationId }));
                    });
  }

  [Test]
  public async Task PutOnTheMenuAsync_NoStationAtAll_FailsBecauseNobodyWouldPrepareTheItem()
  {
    Result<FestivalCatalogItem, FestivalMenuFailure> putOn = await _service.PutOnTheMenuAsync(_festivalId, _bratwurstId, 350, [], CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(putOn.IsSuccess, Is.False);
                      Assert.That(putOn.Failure.Reason, Is.EqualTo(FestivalMenuFailureReason.NoStationPreparesTheItem));
                    });
  }

  [Test]
  public async Task PutOnTheMenuAsync_AnItemThatIsNotOnTheMenuYet_AddsTheRowAndItsStationAndCommits()
  {
    Result<FestivalCatalogItem, FestivalMenuFailure> putOn = await _service.PutOnTheMenuAsync(_festivalId, _bratwurstId, 350, [_kitchenId], CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(putOn.IsSuccess, Is.True);
                      Assert.That(putOn.Value.PriceCents, Is.EqualTo(350));
                      Assert.That(_transactionRunner.Committed, Is.True);
                    });

    A.CallTo(() => _repository.AddMenuRowAsync(A<FestivalCatalogItem>.That.Matches(row => row.PriceCents == 350), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _repository.AddAssignmentAsync(A<ItemStationAssignment>.That.Matches(assignment => assignment.StationId == _kitchenId), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task PutOnTheMenuAsync_TheSameStationTwice_StoresOneAssignmentForIt()
  {
    await _service.PutOnTheMenuAsync(_festivalId,
                                     _bratwurstId,
                                     350,
                                     [
                                       _kitchenId,
                                       _kitchenId
                                     ],
                                     CancellationToken.None);

    A.CallTo(() => _repository.AddAssignmentAsync(A<ItemStationAssignment>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task PutOnTheMenuAsync_AnItemAlreadyOnTheMenu_KeepsTheRowAndTakesTheNewPrice()
  {
    var menuRow = BuildMenuRow(350, true);
    A.CallTo(() => _repository.FindMenuRowAsync(_festivalId, _bratwurstId, A<CancellationToken>._)).Returns(Task.FromResult<FestivalCatalogItem?>(menuRow));

    await _service.PutOnTheMenuAsync(_festivalId, _bratwurstId, 400, [_kitchenId], CancellationToken.None);

    Assert.That(menuRow.PriceCents, Is.EqualTo(400));

    A.CallTo(() => _repository.AddMenuRowAsync(A<FestivalCatalogItem>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task TakeOffTheMenuAsync_AFestivalRunningRightNow_LeavesTheItemOnTheMenu()
  {
    A.CallTo(() => _repository.FindMenuRowAsync(_festivalId, _bratwurstId, A<CancellationToken>._)).Returns(Task.FromResult<FestivalCatalogItem?>(BuildMenuRow(350, true)));

    Result<FestivalCatalogItem, FestivalMenuFailure> takenOff = await _service.TakeOffTheMenuAsync(_festivalId, _bratwurstId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(takenOff.IsSuccess, Is.False);
                      Assert.That(takenOff.Failure.Reason, Is.EqualTo(FestivalMenuFailureReason.FestivalIsRunning));
                      Assert.That(_transactionRunner.Committed, Is.False);
                    });
  }

  [Test]
  public async Task TakeOffTheMenuAsync_AnItemThatIsNotOnTheMenu_FailsBecauseTheRowIsNotFound()
  {
    Result<FestivalCatalogItem, FestivalMenuFailure> takenOff = await _service.TakeOffTheMenuAsync(_festivalId, _bratwurstId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(takenOff.IsSuccess, Is.False);
                      Assert.That(takenOff.Failure.Reason, Is.EqualTo(FestivalMenuFailureReason.MenuRowNotFound));
                    });
  }

  [Test]
  public async Task TakeOffTheMenuAsync_AFestivalThatIsOver_RemovesTheRowAndItsAssignments()
  {
    var menuRow = BuildMenuRow(350, true);
    A.CallTo(() => _repository.FindMenuRowAsync(_festivalId, _bratwurstId, A<CancellationToken>._)).Returns(Task.FromResult<FestivalCatalogItem?>(menuRow));
    A.CallTo(() => _festivalRepository.FindByIdAsync(_festivalId, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(BuildFestival(true)));

    Result<FestivalCatalogItem, FestivalMenuFailure> takenOff = await _service.TakeOffTheMenuAsync(_festivalId, _bratwurstId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(takenOff.IsSuccess, Is.True);
                      Assert.That(_transactionRunner.Committed, Is.True);
                    });

    A.CallTo(() => _repository.RemoveMenuRow(menuRow)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task SetAvailabilityAsync_TheAvailabilityTheItemAlreadyHad_ChangesNothingAndRollsBack()
  {
    A.CallTo(() => _repository.FindMenuRowAsync(_festivalId, _bratwurstId, A<CancellationToken>._)).Returns(Task.FromResult<FestivalCatalogItem?>(BuildMenuRow(350, true)));

    Result<FestivalCatalogItem?, FestivalMenuFailure> saved = await _service.SetAvailabilityAsync(_festivalId, _bratwurstId, true, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(saved.IsSuccess, Is.True);
                      Assert.That(saved.Value, Is.Null);
                      Assert.That(_transactionRunner.Committed, Is.False);
                    });
  }

  [Test]
  public async Task SetAvailabilityAsync_AnItemThatJustSoldOut_MarksItUnavailableAndCommits()
  {
    var menuRow = BuildMenuRow(350, true);
    A.CallTo(() => _repository.FindMenuRowAsync(_festivalId, _bratwurstId, A<CancellationToken>._)).Returns(Task.FromResult<FestivalCatalogItem?>(menuRow));

    Result<FestivalCatalogItem?, FestivalMenuFailure> saved = await _service.SetAvailabilityAsync(_festivalId, _bratwurstId, false, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(saved.Value, Is.SameAs(menuRow));
                      Assert.That(menuRow.IsAvailable, Is.False);
                      Assert.That(_transactionRunner.Committed, Is.True);
                    });
  }

  private FestivalCatalogItem BuildMenuRow(int priceCents, bool isAvailable)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             FestivalId = _festivalId,
             CatalogItemId = _bratwurstId,
             PriceCents = priceCents,
             IsAvailable = isAvailable
           };
  }

  private Festival BuildFestival(bool hasEnded)
  {
    return new()
           {
             Id = _festivalId,
             Name = "Sommerfest",
             StartsAtUtc = _now.AddHours(-5),
             EndsAtUtc = EndOfTheFestival(hasEnded),
             NextOrderNumber = 1,
             IsHidden = false
           };
  }

  private DateTime EndOfTheFestival(bool hasEnded)
  {
    if (hasEnded)
      return _now.AddHours(-1);

    return _now.AddHours(5);
  }
}
