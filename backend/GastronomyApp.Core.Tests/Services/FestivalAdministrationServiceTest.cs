using FakeItEasy;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using GastronomyApp.Core.Tests.TestSupport;
using Microsoft.Extensions.Time.Testing;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class FestivalAdministrationServiceTest
{
  [SetUp]
  public void SetUp()
  {
    _repository = A.Fake<IFestivalRepository>();
    _clock = new FakeTimeProvider(new(_now));
    _transactionRunner = new();

    A.CallTo(() => _repository.FindAllAsync(A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyCollection<Festival>>([]));
    A.CallTo(() => _repository.FindByIdAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(null));
    A.CallTo(() => _repository.ExistsAsync(A<Guid>._, A<CancellationToken>._)).Returns(true);

    _service = new(_repository, new(), new(), _transactionRunner, new(_repository, new(), _clock));
  }

  private readonly DateTime _now = new(2026, 8, 27, 18, 0, 0, DateTimeKind.Utc);
  private readonly Guid _festivalId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");

  private TimeProvider _clock = null!;
  private IFestivalRepository _repository = null!;
  private FestivalAdministrationService _service = null!;
  private RecordingTransactionRunner _transactionRunner = null!;

  [Test]
  public async Task ListAsync_AFestivalNothingHasBeenAddedTo_CountsNothingInsteadOfFailing()
  {
    A.CallTo(() => _repository.FindAllWithContentsAsync(A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<Festival>>([BuildFestival(_festivalId, false)]));

    IReadOnlyList<Festival> listed = await _service.ListAsync(CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(listed[0].StationCount(), Is.EqualTo(0));
                      Assert.That(listed[0].MenuItemCount(), Is.EqualTo(0));
                      Assert.That(_service.IsRunning(listed[0]), Is.True);
                    });
  }

  [Test]
  public async Task ListAsync_AFestivalThatIsHidden_IsNotRunning()
  {
    A.CallTo(() => _repository.FindAllWithContentsAsync(A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<Festival>>([BuildFestival(_festivalId, true)]));

    IReadOnlyList<Festival> listed = await _service.ListAsync(CancellationToken.None);

    Assert.That(_service.IsRunning(listed[0]), Is.False);
  }

  [Test]
  public async Task CountOrdersByFestivalAsync_AFestivalWithOrders_CountsThem()
  {
    A.CallTo(() => _repository.CountOrdersByFestivalAsync(A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyDictionary<Guid, int>>(new Dictionary<Guid, int> { [_festivalId] = 13 }));

    IReadOnlyDictionary<Guid, int> counted = await _service.CountOrdersByFestivalAsync(CancellationToken.None);

    Assert.That(counted[_festivalId], Is.EqualTo(13));
  }

  [Test]
  public async Task CreateAsync_ANameOfOnlySpaces_FailsBecauseTheNameIsMissing()
  {
    Result<Festival, FestivalAdministrationFailure> created = await _service.CreateAsync("   ", _now, _now.AddHours(6), CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.IsSuccess, Is.False);
                      Assert.That(created.Failure.Reason, Is.EqualTo(FestivalAdministrationFailureReason.NameMissing));
                      Assert.That(_transactionRunner.Committed, Is.False);
                    });
  }

  [Test]
  public async Task CreateAsync_AnEndThatIsNotAfterTheStart_FailsBecauseThePeriodIsInvalid()
  {
    Result<Festival, FestivalAdministrationFailure> created = await _service.CreateAsync("Sommerfest", _now, _now, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.IsSuccess, Is.False);
                      Assert.That(created.Failure.Reason, Is.EqualTo(FestivalAdministrationFailureReason.PeriodInvalid));
                    });
  }

  [Test]
  public async Task CreateAsync_APeriodAnotherFestivalAlreadyCovers_NamesTheFestivalInTheWay()
  {
    A.CallTo(() => _repository.FindAllAsync(A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyCollection<Festival>>([BuildFestival(_festivalId, false)]));

    Result<Festival, FestivalAdministrationFailure> created = await _service.CreateAsync("Herbstfest", _now, _now.AddHours(2), CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.IsSuccess, Is.False);
                      Assert.That(created.Failure.Reason, Is.EqualTo(FestivalAdministrationFailureReason.PeriodOverlapsAnotherFestival));
                      Assert.That(created.Failure.OverlappingFestivalName, Is.EqualTo("Sommerfest"));
                    });
  }

  [Test]
  public async Task CreateAsync_APeriodNoOtherFestivalCovers_StoresTheFestivalAndCommits()
  {
    Result<Festival, FestivalAdministrationFailure> created = await _service.CreateAsync("Sommerfest", _now, _now.AddHours(6), CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.IsSuccess, Is.True);
                      Assert.That(created.Value.Name, Is.EqualTo("Sommerfest"));
                      Assert.That(_transactionRunner.Committed, Is.True);
                    });

    A.CallTo(() => _repository.AddAsync(A<Festival>.That.Matches(festival => festival.Name == "Sommerfest"), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task CopyAsync_AFestivalThatIsNotThere_FailsBecauseTheFestivalIsNotFound()
  {
    A.CallTo(() => _repository.ExistsAsync(_festivalId, A<CancellationToken>._)).Returns(false);

    Result<Festival, FestivalAdministrationFailure> copied = await _service.CopyAsync(_festivalId, "Sommerfest 2027", _now, _now.AddHours(6), CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(copied.IsSuccess, Is.False);
                      Assert.That(copied.Failure.Reason, Is.EqualTo(FestivalAdministrationFailureReason.FestivalNotFound));
                    });
  }

  [Test]
  public async Task CopyAsync_AFestivalThatIsThere_CopiesItsStationsItemsAndAssignmentsAcross()
  {
    Result<Festival, FestivalAdministrationFailure> copied = await _service.CopyAsync(_festivalId, "Sommerfest 2027", _now, _now.AddHours(6), CancellationToken.None);

    Assert.That(copied.IsSuccess, Is.True);

    A.CallTo(() => _repository.CopyContentsAsync(_festivalId, copied.Value.Id, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task HideAsync_AFestivalRunningRightNow_RefusesAndNamesTheFestival()
  {
    A.CallTo(() => _repository.FindByIdAsync(_festivalId, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(BuildFestival(_festivalId, false)));

    Result<Festival?, FestivalAdministrationFailure> hidden = await _service.HideAsync(_festivalId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(hidden.IsSuccess, Is.False);
                      Assert.That(hidden.Failure.Reason, Is.EqualTo(FestivalAdministrationFailureReason.FestivalIsRunning));
                      Assert.That(hidden.Failure.OffendingFestivalId, Is.EqualTo(_festivalId));
                      Assert.That(_transactionRunner.Committed, Is.False);
                    });
  }

  [Test]
  public async Task HideAsync_AFestivalThatIsAlreadyHidden_ChangesNothingAndRollsBack()
  {
    A.CallTo(() => _repository.FindByIdAsync(_festivalId, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(BuildFestival(_festivalId, true)));

    Result<Festival?, FestivalAdministrationFailure> hidden = await _service.HideAsync(_festivalId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(hidden.IsSuccess, Is.True);
                      Assert.That(hidden.Value, Is.Null);
                      Assert.That(_transactionRunner.Committed, Is.False);
                    });
  }

  [Test]
  public async Task ShowAsync_AFestivalThatIsHidden_ShowsItAgainAndCommits()
  {
    var festival = BuildFestival(_festivalId, true);
    A.CallTo(() => _repository.FindByIdAsync(_festivalId, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(festival));

    Result<Festival?, FestivalAdministrationFailure> shown = await _service.ShowAsync(_festivalId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(shown.IsSuccess, Is.True);
                      Assert.That(shown.Value, Is.SameAs(festival));
                      Assert.That(festival.IsHidden, Is.False);
                      Assert.That(_transactionRunner.Committed, Is.True);
                    });
  }

  [Test]
  public async Task UpdateAsync_AFestivalThatIsNotThere_FailsBecauseTheFestivalIsNotFound()
  {
    Result<Festival, FestivalAdministrationFailure> updated = await _service.UpdateAsync(_festivalId, "Sommerfest", _now, _now.AddHours(6), CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(updated.IsSuccess, Is.False);
                      Assert.That(updated.Failure.Reason, Is.EqualTo(FestivalAdministrationFailureReason.FestivalNotFound));
                    });
  }

  private Festival BuildFestival(Guid festivalId, bool isHidden)
  {
    return new()
           {
             Id = festivalId,
             Name = "Sommerfest",
             StartsAtUtc = _now.AddHours(-1),
             EndsAtUtc = _now.AddHours(5),
             NextOrderNumber = 1,
             IsHidden = isHidden
           };
  }
}
