using FakeItEasy;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using GastronomyApp.Core.Tests.TestSupport;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class StationAdministrationServiceTest
{
  [SetUp]
  public void SetUp()
  {
    _repository = A.Fake<IStationRepository>();
    _festivalRepository = A.Fake<IFestivalRepository>();
    _festivalStationRepository = A.Fake<IFestivalStationRepository>();
    _invitationStore = A.Fake<IEnrolmentInvitationStore>();
    _deviceTokenStore = A.Fake<IDeviceTokenStore>();
    _orderabilityRepository = A.Fake<IItemOrderabilityRepository>();
    _clock = A.Fake<IClock>();
    _transactionRunner = new();

    A.CallTo(() => _clock.UtcNow).Returns(_now);
    A.CallTo(() => _festivalRepository.ExistsAsync(A<Guid>._, A<CancellationToken>._)).Returns(true);
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._))
     .Returns(Task.FromResult<Festival?>(null));
    A.CallTo(() => _festivalRepository.FindIdsNotEndedAsync(A<DateTime>._, A<CancellationToken>._))
     .Returns(Task.FromResult<IReadOnlyList<Guid>>([]));
    A.CallTo(() => _repository.FindByIdAsync(A<Guid>._, A<CancellationToken>._))
     .Returns(Task.FromResult<Station?>(null));
    A.CallTo(() => _repository.FindAdministeredAsync(A<Guid?>._, A<DateTime>._, A<CancellationToken>._))
     .Returns(Task.FromResult<IReadOnlyList<AdministeredStation>>([]));
    A.CallTo(() => _festivalStationRepository.CountUnfulfilledItemsAsync(A<Guid>._,
                                                                         A<Guid>._,
                                                                         A<CancellationToken>._)).Returns(0);

    _service = new(_repository,
                   _festivalRepository,
                   _festivalStationRepository,
                   new(_invitationStore, _deviceTokenStore, _clock),
                   new(_orderabilityRepository, _festivalRepository, _clock),
                   _transactionRunner,
                   _clock);
  }

  private readonly DateTime _now = new(2026, 8, 27, 18, 0, 0, DateTimeKind.Utc);
  private readonly Guid _deviceId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
  private readonly Guid _festivalId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
  private readonly Guid _invitationId = Guid.Parse("ffffffff-0000-0000-0000-000000000001");
  private readonly Guid _kitchenId = Guid.Parse("dddddddd-0000-0000-0000-000000000001");

  private IClock _clock = null!;
  private IDeviceTokenStore _deviceTokenStore = null!;
  private IFestivalRepository _festivalRepository = null!;
  private IFestivalStationRepository _festivalStationRepository = null!;
  private IEnrolmentInvitationStore _invitationStore = null!;
  private IItemOrderabilityRepository _orderabilityRepository = null!;
  private IStationRepository _repository = null!;
  private StationAdministrationService _service = null!;
  private RecordingTransactionRunner _transactionRunner = null!;

  [Test]
  public void CreateAsync_NullRequest_ThrowsArgumentNullException()
  {
    Assert.That(async () => await _service.CreateAsync(null!, CancellationToken.None),
                Throws.ArgumentNullException);
  }

  [Test]
  public void UpdateAsync_NullRequest_ThrowsArgumentNullException()
  {
    Assert.That(async () => await _service.UpdateAsync(_kitchenId, null!, CancellationToken.None),
                Throws.ArgumentNullException);
  }

  [Test]
  public async Task ListAsync_AFestivalThatIsNotThere_FailsBecauseTheFestivalIsNotFound()
  {
    A.CallTo(() => _festivalRepository.ExistsAsync(_festivalId, A<CancellationToken>._)).Returns(false);

    Result<IReadOnlyList<AdministeredStation>, StationAdministrationFailure> listed =
      await _service.ListAsync(_festivalId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(listed.IsSuccess, Is.False);
                      Assert.That(listed.Failure.Reason,
                                  Is.EqualTo(StationAdministrationFailureReason.FestivalNotFound));
                    });
  }

  [Test]
  public async Task CreateAsync_ANameOfOnlySpaces_FailsBecauseTheNameIsMissing()
  {
    Result<SavedStation, StationAdministrationFailure> created =
      await _service.CreateAsync(BuildRequest("  ", 1), CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.IsSuccess, Is.False);
                      Assert.That(created.Failure.Reason,
                                  Is.EqualTo(StationAdministrationFailureReason.NameMissing));
                      Assert.That(_transactionRunner.Committed, Is.False);
                    });
  }

  [Test]
  public async Task UpdateAsync_TheNameAndPlaceTheStationAlreadyHad_ReportsThatNothingChanged()
  {
    A.CallTo(() => _repository.FindByIdAsync(_kitchenId, A<CancellationToken>._))
     .Returns(Task.FromResult<Station?>(BuildStation(true, null, null)));

    Result<SavedStation, StationAdministrationFailure> updated =
      await _service.UpdateAsync(_kitchenId, BuildRequest("Kueche", 1), CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(updated.IsSuccess, Is.True);
                      Assert.That(updated.Value.SomethingChanged, Is.False);
                    });
  }

  [Test]
  public async Task UpdateAsync_ANameTheStationDidNotHaveBefore_ReportsThatSomethingChanged()
  {
    A.CallTo(() => _repository.FindByIdAsync(_kitchenId, A<CancellationToken>._))
     .Returns(Task.FromResult<Station?>(BuildStation(true, null, null)));

    Result<SavedStation, StationAdministrationFailure> updated =
      await _service.UpdateAsync(_kitchenId, BuildRequest("Kueche am Zelt", 1), CancellationToken.None);

    Assert.That(updated.Value.SomethingChanged, Is.True);
  }

  [Test]
  public async Task ActivateAsync_AStationThatWasAlreadySwitchedOn_ReportsThatNothingChanged()
  {
    A.CallTo(() => _repository.FindByIdAsync(_kitchenId, A<CancellationToken>._))
     .Returns(Task.FromResult<Station?>(BuildStation(true, null, null)));

    Result<SavedStation, StationAdministrationFailure> switchedOn =
      await _service.ActivateAsync(_kitchenId, CancellationToken.None);

    Assert.That(switchedOn.Value.SomethingChanged, Is.False);
  }

  [Test]
  public async Task DeactivateAsync_AStationThatIsNotThere_FailsBecauseTheStationIsNotFound()
  {
    Result<SavedStation, StationAdministrationFailure> switchedOff =
      await _service.DeactivateAsync(_kitchenId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(switchedOff.IsSuccess, Is.False);
                      Assert.That(switchedOff.Failure.Reason,
                                  Is.EqualTo(StationAdministrationFailureReason.StationNotFound));
                    });
  }

  [Test]
  public async Task DeactivateAsync_AStationWithOpenItemsAtTheRunningFestival_LeavesItSwitchedOn()
  {
    Station station = BuildStation(true, null, null);
    A.CallTo(() => _repository.FindByIdAsync(_kitchenId, A<CancellationToken>._))
     .Returns(Task.FromResult<Station?>(station));
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._))
     .Returns(Task.FromResult<Festival?>(BuildFestival()));
    A.CallTo(() => _festivalStationRepository.CountUnfulfilledItemsAsync(_festivalId,
                                                                         _kitchenId,
                                                                         A<CancellationToken>._)).Returns(2);

    Result<SavedStation, StationAdministrationFailure> switchedOff =
      await _service.DeactivateAsync(_kitchenId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(switchedOff.IsSuccess, Is.False);
                      Assert.That(switchedOff.Failure.Reason,
                                  Is.EqualTo(StationAdministrationFailureReason.StationHasUnfulfilledItems));
                      Assert.That(station.IsActive, Is.True);
                    });
  }

  [Test]
  public async Task DeactivateAsync_AStationNoItemNeeds_WithdrawsItsInvitationAndRevokesItsDevice()
  {
    Station station = BuildStation(true, _deviceId, _invitationId);
    A.CallTo(() => _repository.FindByIdAsync(_kitchenId, A<CancellationToken>._))
     .Returns(Task.FromResult<Station?>(station));

    Result<SavedStation, StationAdministrationFailure> switchedOff =
      await _service.DeactivateAsync(_kitchenId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(switchedOff.IsSuccess, Is.True);
                      Assert.That(switchedOff.Value.SomethingChanged, Is.True);
                      Assert.That(switchedOff.Value.RevokedDeviceId, Is.EqualTo(_deviceId));
                      Assert.That(station.IsActive, Is.False);
                      Assert.That(station.EnrolmentInvitationId, Is.Null);
                      Assert.That(_transactionRunner.Committed, Is.True);
                    });

    A.CallTo(() => _invitationStore.ConsumeAsync(_invitationId, _now, A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
    A.CallTo(() => _deviceTokenStore.RevokeAsync(_deviceId, A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task DeactivateAsync_AStationWithoutADevice_RevokesNothing()
  {
    A.CallTo(() => _repository.FindByIdAsync(_kitchenId, A<CancellationToken>._))
     .Returns(Task.FromResult<Station?>(BuildStation(true, null, null)));

    Result<SavedStation, StationAdministrationFailure> switchedOff =
      await _service.DeactivateAsync(_kitchenId, CancellationToken.None);

    Assert.That(switchedOff.Value.RevokedDeviceId, Is.Null);

    A.CallTo(() => _deviceTokenStore.RevokeAsync(A<Guid>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  private SaveStationNameRequest BuildRequest(string? name, int sortOrder)
  {
    return new()
           {
             Name = name,
             SortOrder = sortOrder
           };
  }

  private Station BuildStation(bool isActive, Guid? deviceId, Guid? invitationId)
  {
    return new()
           {
             Id = _kitchenId,
             Name = "Kueche",
             SortOrder = 1,
             IsActive = isActive,
             DeviceId = deviceId,
             EnrolmentInvitationId = invitationId
           };
  }

  private Festival BuildFestival()
  {
    return new()
           {
             Id = _festivalId,
             Name = "Sommerfest",
             StartsAtUtc = _now.AddHours(-1),
             EndsAtUtc = _now.AddHours(5),
             NextOrderNumber = 1,
             IsHidden = false
           };
  }
}
