using FakeItEasy;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using GastronomyApp.Core.Tests.TestSupport;
using Microsoft.Extensions.Time.Testing;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class StaffMemberAdministrationServiceTest
{
  [SetUp]
  public void SetUp()
  {
    _repository = A.Fake<IStaffMemberRepository>();
    _invitationStore = A.Fake<IEnrolmentInvitationStore>();
    _deviceTokenStore = A.Fake<IDeviceTokenStore>();
    _announcer = A.Fake<IDeviceRevocationAnnouncer>();
    _clock = new FakeTimeProvider(new(_now));
    _transactionRunner = new();

    A.CallTo(() => _repository.FindByIdAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<StaffMember?>(null));

    _service = new(_repository, new(_invitationStore, _deviceTokenStore, _announcer, new ImmediateAfterCommitActions(), _clock), _transactionRunner);
  }

  private readonly DateTime _now = new(2026, 8, 27, 18, 0, 0, DateTimeKind.Utc);
  private readonly Guid _annaId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
  private readonly Guid _deviceId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
  private readonly Guid _invitationId = Guid.Parse("ffffffff-0000-0000-0000-000000000001");

  private IDeviceRevocationAnnouncer _announcer = null!;
  private TimeProvider _clock = null!;
  private IDeviceTokenStore _deviceTokenStore = null!;
  private IEnrolmentInvitationStore _invitationStore = null!;
  private IStaffMemberRepository _repository = null!;
  private StaffMemberAdministrationService _service = null!;
  private RecordingTransactionRunner _transactionRunner = null!;

  [Test]
  public async Task ListAsync_AskedForEverybody_ReadsTheListAsItIsRightNow()
  {
    await _service.ListAsync(CancellationToken.None);

    A.CallTo(() => _repository.FindAllAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task RenameAsync_ASomebodyWhoIsNotOnTheList_FailsBecauseThePersonIsNotFound()
  {
    Result<StaffMember, Failure<StaffMemberAdministrationFailureReason>> renamed = await _service.RenameAsync(_annaId, "Annemarie", CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(renamed.IsSuccess, Is.False);
                      Assert.That(renamed.Failure.Reason, Is.EqualTo(StaffMemberAdministrationFailureReason.StaffMemberNotFound));
                    });
  }

  [Test]
  public async Task RenameAsync_ANewName_KeepsExactlyWhatWasTypedAndCommits()
  {
    var staffMember = BuildStaffMember(true, null, null);
    A.CallTo(() => _repository.FindByIdAsync(_annaId, A<CancellationToken>._)).Returns(Task.FromResult<StaffMember?>(staffMember));

    Result<StaffMember, Failure<StaffMemberAdministrationFailureReason>> renamed = await _service.RenameAsync(_annaId, "Anne Marie", CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(renamed.IsSuccess, Is.True);
                      Assert.That(renamed.Value.Name, Is.EqualTo("Anne Marie"));
                      Assert.That(staffMember.Name, Is.EqualTo("Anne Marie"));
                      Assert.That(_transactionRunner.Committed, Is.True);
                    });
  }

  [Test]
  public async Task ActivateAsync_ASomebodyWhoWasTakenOffTheList_PutsThemBackOnIt()
  {
    var staffMember = BuildStaffMember(false, null, null);
    A.CallTo(() => _repository.FindByIdAsync(_annaId, A<CancellationToken>._)).Returns(Task.FromResult<StaffMember?>(staffMember));

    Result<StaffMember, Failure<StaffMemberAdministrationFailureReason>> switchedOn = await _service.ActivateAsync(_annaId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(switchedOn.IsSuccess, Is.True);
                      Assert.That(staffMember.IsActive, Is.True);
                    });
  }

  [Test]
  public async Task DeactivateAsync_ASomebodyHoldingAPhone_WithdrawsTheInvitationAndRevokesThePhone()
  {
    var staffMember = BuildStaffMember(true, _deviceId, _invitationId);
    A.CallTo(() => _repository.FindByIdAsync(_annaId, A<CancellationToken>._)).Returns(Task.FromResult<StaffMember?>(staffMember));

    Result<StaffMember, Failure<StaffMemberAdministrationFailureReason>> switchedOff = await _service.DeactivateAsync(_annaId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(switchedOff.IsSuccess, Is.True);
                      Assert.That(staffMember.IsActive, Is.False);
                      Assert.That(staffMember.EnrolmentInvitationId, Is.Null);
                      Assert.That(_transactionRunner.Committed, Is.True);
                    });

    A.CallTo(() => _invitationStore.ConsumeAsync(_invitationId, _now, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _deviceTokenStore.RevokeAsync(_deviceId, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _announcer.AnnounceAsync(_deviceId, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task DeactivateAsync_ASomebodyWithoutAPhone_RevokesNothing()
  {
    A.CallTo(() => _repository.FindByIdAsync(_annaId, A<CancellationToken>._)).Returns(Task.FromResult<StaffMember?>(BuildStaffMember(true, null, null)));

    Result<StaffMember, Failure<StaffMemberAdministrationFailureReason>> switchedOff = await _service.DeactivateAsync(_annaId, CancellationToken.None);

    Assert.That(switchedOff.IsSuccess, Is.True);

    A.CallTo(() => _deviceTokenStore.RevokeAsync(A<Guid>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _announcer.AnnounceAsync(A<Guid>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  private StaffMember BuildStaffMember(bool isActive, Guid? deviceId, Guid? invitationId)
  {
    return new()
           {
             Id = _annaId,
             Name = "Anna",
             IsActive = isActive,
             DeviceId = deviceId,
             EnrolmentInvitationId = invitationId,
             CreatedAtUtc = _now
           };
  }
}
