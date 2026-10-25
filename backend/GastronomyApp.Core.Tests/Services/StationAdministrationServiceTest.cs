using ErrorOr;
using FakeItEasy;
using GastronomyApp.Core.Announcements;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using GastronomyApp.Core.Tests.TestSupport;
using Microsoft.Extensions.Time.Testing;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class StationAdministrationServiceTest
{
  [SetUp]
  public void SetUp()
  {
    _repository = A.Fake<IStationRepository>();
    _festivalRepository = A.Fake<IFestivalRepository>();
    _invitationStore = A.Fake<IEnrolmentInvitationStore>();
    _deviceTokenStore = A.Fake<IDeviceTokenStore>();
    _announcer = A.Fake<IDeviceRevocationAnnouncer>();
    _orderabilityRepository = A.Fake<IItemOrderabilityRepository>();
    _clock = new FakeTimeProvider(new(_now));

    A.CallTo(() => _festivalRepository.FindByIdAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(BuildFestival(_now.AddHours(5))));
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(null));
    A.CallTo(() => _festivalRepository.FindIdsNotEndedAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<Guid>>([]));
    A.CallTo(() => _repository.FindByIdAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<Station?>(null));
    A.CallTo(() => _repository.FindStationIdsWithItemsToHandOutAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyCollection<Guid>>([]));

    _service = new(_repository,
                   _festivalRepository,
                   new(_invitationStore, _deviceTokenStore, _announcer, new ImmediateAfterCommitActions(A.Fake<ICommittedChangeAnnouncer>()), _clock),
                   new(_orderabilityRepository, _festivalRepository, _clock),
                   new(_repository, A.Fake<ITablePlanRepository>(), new(_festivalRepository, new(), _clock)));
  }

  private readonly StationService _stationService = new();
  private readonly DateTime _now = new(2026, 8, 27, 18, 0, 0, DateTimeKind.Utc);
  private readonly Guid _deviceId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
  private readonly Guid _festivalId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
  private readonly Guid _invitationId = Guid.Parse("ffffffff-0000-0000-0000-000000000001");
  private readonly Guid _kitchenId = Guid.Parse("dddddddd-0000-0000-0000-000000000001");

  private IDeviceRevocationAnnouncer _announcer = null!;
  private TimeProvider _clock = null!;
  private IDeviceTokenStore _deviceTokenStore = null!;
  private IFestivalRepository _festivalRepository = null!;
  private IEnrolmentInvitationStore _invitationStore = null!;
  private IItemOrderabilityRepository _orderabilityRepository = null!;
  private IStationRepository _repository = null!;
  private StationAdministrationService _service = null!;

  [Test]
  public async Task ListAsync_AFestivalThatIsNotThere_FailsBecauseTheFestivalIsNotFound()
  {
    A.CallTo(() => _festivalRepository.FindByIdAsync(_festivalId, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(null));

    ErrorOr<IReadOnlyList<ListedStation>> listed = await _service.ListAsync(_festivalId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(listed.IsSuccess, Is.False);
                      Assert.That(listed.RefusalMessageKey(), Is.EqualTo("FestivalNotFound"));
                    });
  }

  [Test]
  public async Task ListAsync_AFestivalThatRuns_ReadsTheItemsToHandOutAtThatFestival()
  {
    GivenTheStations(_festivalId, BuildStation(true, null, null));
    A.CallTo(() => _repository.FindStationIdsWithItemsToHandOutAsync(_festivalId, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyCollection<Guid>>([_kitchenId]));

    ErrorOr<IReadOnlyList<ListedStation>> listed = await _service.ListAsync(_festivalId, CancellationToken.None);

    Assert.That(listed.Value.Single().HasItemsToHandOutAtTheRunningFestival, Is.True);
  }

  [Test]
  public async Task ListAsync_AFestivalThatIsOver_SaysNoStationHasItemsToHandOut()
  {
    GivenTheStations(_festivalId, BuildStation(true, null, null));
    A.CallTo(() => _festivalRepository.FindByIdAsync(_festivalId, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(BuildFestival(_now.AddHours(-1))));
    A.CallTo(() => _repository.FindStationIdsWithItemsToHandOutAsync(_festivalId, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyCollection<Guid>>([_kitchenId]));

    ErrorOr<IReadOnlyList<ListedStation>> listed = await _service.ListAsync(_festivalId, CancellationToken.None);

    Assert.That(listed.Value.Single().HasItemsToHandOutAtTheRunningFestival, Is.False);
  }

  [Test]
  public async Task ListAsync_NoFestivalWhileOneRuns_ReadsTheItemsToHandOutAtTheRunningFestival()
  {
    GivenTheStations(null, BuildStation(true, null, null));
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(BuildFestival(_now.AddHours(5))));
    A.CallTo(() => _repository.FindStationIdsWithItemsToHandOutAsync(_festivalId, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyCollection<Guid>>([_kitchenId]));

    ErrorOr<IReadOnlyList<ListedStation>> listed = await _service.ListAsync(null, CancellationToken.None);

    Assert.That(listed.Value.Single().HasItemsToHandOutAtTheRunningFestival, Is.True);
  }

  [Test]
  public async Task ListAsync_NoFestivalWhileNoneRuns_SaysNoStationHasItemsToHandOut()
  {
    GivenTheStations(null, BuildStation(true, null, null));

    ErrorOr<IReadOnlyList<ListedStation>> listed = await _service.ListAsync(null, CancellationToken.None);

    Assert.That(listed.Value.Single().HasItemsToHandOutAtTheRunningFestival, Is.False);

    A.CallTo(() => _repository.FindStationIdsWithItemsToHandOutAsync(A<Guid>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task ListAsync_NoFestival_CountsTheArticlesSwitchingTheStationOffWouldStrand()
  {
    GivenTheStations(null, BuildStation(true, null, null));
    GivenTheKitchenAloneMakesTheBratwurst();
    A.CallTo(() => _festivalRepository.FindIdsNotEndedAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<Guid>>([_festivalId]));

    ErrorOr<IReadOnlyList<ListedStation>> listed = await _service.ListAsync(null, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(listed.Value.Single().StrandedItemCountWhenSwitchedOff, Is.EqualTo(1));
                      Assert.That(listed.Value.Single().StrandedItemCountWhenRemovedFromTheFestival, Is.Null);
                    });
  }

  [Test]
  public async Task ListAsync_AFestival_CountsTheArticlesRemovingTheStationFromItWouldStrand()
  {
    GivenTheStations(_festivalId, BuildStation(true, null, null));
    GivenTheKitchenAloneMakesTheBratwurst();

    ErrorOr<IReadOnlyList<ListedStation>> listed = await _service.ListAsync(_festivalId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(listed.Value.Single().StrandedItemCountWhenRemovedFromTheFestival, Is.EqualTo(1));
                      Assert.That(listed.Value.Single().StrandedItemCountWhenSwitchedOff, Is.Null);
                    });
  }

  [Test]
  public async Task ListAsync_NoFestivalWithSeveralStations_ReadsTheFestivalsNotYetOverOnce()
  {
    GivenTheStations(null,
                     BuildStation(true, null, null),
                     new()
                     {
                       Id = Guid.NewGuid(),
                       Name = "Theke",
                       SortOrder = 2,
                       IsActive = true
                     });

    await _service.ListAsync(null, CancellationToken.None);

    A.CallTo(() => _festivalRepository.FindIdsNotEndedAsync(A<DateTime>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task CreateAsync_AValidRequest_HandsBackTheStationTheAdminListShows()
  {
    ErrorOr<ListedStation> created = await _service.CreateAsync("Kueche am Zelt", 2, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.IsSuccess, Is.True);
                      Assert.That(created.Value.Station.Id, Is.Not.EqualTo(Guid.Empty));
                      Assert.That(created.Value.Station.Name, Is.EqualTo("Kueche am Zelt"));
                      Assert.That(created.Value.Station.SortOrder, Is.EqualTo(2));
                      Assert.That(created.Value.Station.IsActive, Is.True);
                      Assert.That(created.Value.Station.DeviceId, Is.Null);
                      Assert.That(_stationService.IsAtAnyFestival(created.Value.Station), Is.False);
                      Assert.That(created.Value.HasItemsToHandOutAtTheRunningFestival, Is.False);
                      Assert.That(created.Value.StrandedItemCountWhenSwitchedOff, Is.Zero);
                      Assert.That(created.Value.StrandedItemCountWhenRemovedFromTheFestival, Is.Zero);
                    });
  }

  [Test]
  public async Task UpdateAsync_ANameTheStationDidNotHaveBefore_HandsBackTheChangedStation()
  {
    A.CallTo(() => _repository.FindByIdAsync(_kitchenId, A<CancellationToken>._)).Returns(Task.FromResult<Station?>(BuildStation(true, null, null)));

    ErrorOr<Station> updated = await _service.UpdateAsync(_kitchenId, "Kueche am Zelt", 1, CancellationToken.None);

    Assert.That(updated.Value, Is.Not.Null);
  }

  [Test]
  public async Task DeactivateAsync_AStationThatIsNotThere_FailsBecauseTheStationIsNotFound()
  {
    ErrorOr<Station> switchedOff = await _service.DeactivateAsync(_kitchenId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(switchedOff.IsSuccess, Is.False);
                      Assert.That(switchedOff.RefusalMessageKey(), Is.EqualTo("StationNotFound"));
                    });
  }

  [Test]
  public async Task DeactivateAsync_AStationWithOpenItemsAtTheRunningFestival_LeavesItSwitchedOn()
  {
    var station = BuildStation(true, null, null);
    A.CallTo(() => _repository.FindByIdAsync(_kitchenId, A<CancellationToken>._)).Returns(Task.FromResult<Station?>(station));
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(BuildFestival(_now.AddHours(5))));
    A.CallTo(() => _repository.FindStationIdsWithItemsToHandOutAsync(_festivalId, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyCollection<Guid>>([_kitchenId]));

    ErrorOr<Station> switchedOff = await _service.DeactivateAsync(_kitchenId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(switchedOff.IsSuccess, Is.False);
                      Assert.That(switchedOff.RefusalMessageKey(), Is.EqualTo("errors.admin.stations.hasUnfinishedItems"));
                      Assert.That(station.IsActive, Is.True);
                    });
  }

  [Test]
  public async Task DeactivateAsync_AStationNoItemNeeds_WithdrawsItsInvitationAndRevokesItsDevice()
  {
    var station = BuildStation(true, _deviceId, _invitationId);
    A.CallTo(() => _repository.FindByIdAsync(_kitchenId, A<CancellationToken>._)).Returns(Task.FromResult<Station?>(station));

    ErrorOr<Station> switchedOff = await _service.DeactivateAsync(_kitchenId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(switchedOff.IsSuccess, Is.True);
                      Assert.That(switchedOff.Value, Is.SameAs(station));
                      Assert.That(station.IsActive, Is.False);
                      Assert.That(station.EnrolmentInvitationId, Is.Null);
                    });

    A.CallTo(() => _invitationStore.ConsumeAsync(_invitationId, _now, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _deviceTokenStore.RevokeAsync(_deviceId, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _announcer.AnnounceDeviceRevokedAsync(_deviceId, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task DeactivateAsync_AStationWithoutADevice_RevokesNothing()
  {
    A.CallTo(() => _repository.FindByIdAsync(_kitchenId, A<CancellationToken>._)).Returns(Task.FromResult<Station?>(BuildStation(true, null, null)));

    ErrorOr<Station> switchedOff = await _service.DeactivateAsync(_kitchenId, CancellationToken.None);

    Assert.That(switchedOff.IsSuccess, Is.True);

    A.CallTo(() => _deviceTokenStore.RevokeAsync(A<Guid>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _announcer.AnnounceDeviceRevokedAsync(A<Guid>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  private void GivenTheStations(Guid? festivalId, params Station[] stations)
  {
    A.CallTo(() => _repository.FindAllAsync(festivalId, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<Station>>(stations));
  }

  private void GivenTheKitchenAloneMakesTheBratwurst()
  {
    var bratwurstId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    A.CallTo(() => _orderabilityRepository.FindActiveStationIdsAtFestivalAsync(_festivalId, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<Guid>>([_kitchenId]));
    A.CallTo(() => _orderabilityRepository.FindActiveMenuItemIdsAsync(_festivalId, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<Guid>>([bratwurstId]));
    A.CallTo(() => _orderabilityRepository.FindItemIdsPreparedByAsync(_festivalId, A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<Guid>>([]));
    A.CallTo(() => _orderabilityRepository.FindItemIdsPreparedByAsync(_festivalId, A<IReadOnlyCollection<Guid>>.That.Contains(_kitchenId), A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<Guid>>([bratwurstId]));
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

  private Festival BuildFestival(DateTime endsAtUtc)
  {
    return new()
           {
             Id = _festivalId,
             Name = "Sommerfest",
             StartsAtUtc = _now.AddHours(-6),
             EndsAtUtc = endsAtUtc,
             NextOrderNumber = 1,
             IsHidden = false
           };
  }
}
