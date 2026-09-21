using ErrorOr;
using FakeItEasy;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using GastronomyApp.Core.Tests.TestSupport;
using Microsoft.Extensions.Time.Testing;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class EnrolmentInvitationServiceTest
{
  [SetUp]
  public void SetUp()
  {
    _store = A.Fake<IEnrolmentInvitationStore>();
    _ownerStore = A.Fake<IDeviceOwnerStore>();
    _deviceTokenStore = A.Fake<IDeviceTokenStore>();
    _announcer = A.Fake<IDeviceRevocationAnnouncer>();
    _clock = new FakeTimeProvider(new(_now));

    A.CallTo(() => _store.CreateAsync(A<IDeviceOwner?>._, A<CancellationToken>._)).ReturnsLazily(call => Task.FromResult(new IssuedEnrolmentInvitation(BuildInvitation(null, null, _now.AddMinutes(5)), "ABCDEF", call.GetArgument<IDeviceOwner?>(0))));
    A.CallTo(() => _ownerStore.FindAsync(A<DeviceOwnerKind>._, A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<IDeviceOwner?>(null));
    A.CallTo(() => _store.FindByIdAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<EnrolmentInvitation?>(null));

    _service = new(_store, _ownerStore, _deviceTokenStore, new(_store, _deviceTokenStore, _announcer, new ImmediateAfterCommitActions(), _clock), _clock);
  }

  private readonly DateTime _now = new(2026, 8, 27, 18, 0, 0, DateTimeKind.Utc);
  private readonly Guid _deviceId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
  private readonly Guid _invitationId = Guid.Parse("ffffffff-0000-0000-0000-000000000001");
  private readonly Guid _staffMemberId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
  private readonly Guid _stationId = Guid.Parse("dddddddd-0000-0000-0000-000000000001");

  private IDeviceRevocationAnnouncer _announcer = null!;
  private TimeProvider _clock = null!;
  private IDeviceTokenStore _deviceTokenStore = null!;
  private IDeviceOwnerStore _ownerStore = null!;
  private EnrolmentInvitationService _service = null!;
  private IEnrolmentInvitationStore _store = null!;

  [Test]
  public void RedeemAsync_NullCode_ThrowsArgumentNullException()
  {
    Assert.That(async () => await _service.RedeemAsync(null!, null, "Test agent", "de", CancellationToken.None), Throws.ArgumentNullException);
  }

  [Test]
  public async Task CreateAsync_BothAWaiterAndAStation_FailsBecauseOnlyOneOwnerIsAllowed()
  {
    ErrorOr<IssuedEnrolmentInvitation> issued = await _service.CreateAsync(_staffMemberId, _stationId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(issued.IsSuccess, Is.False);
                      Assert.That(issued.RefusalMessageKey(), Is.EqualTo("enrolment.atMostOneOwner"));
                    });

    A.CallTo(() => _store.CreateAsync(A<IDeviceOwner?>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task CreateAsync_AnOwnerWhoIsNotOnTheList_FailsBecauseTheOwnerIsNotFound()
  {
    ErrorOr<IssuedEnrolmentInvitation> issued = await _service.CreateAsync(_staffMemberId, null, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(issued.IsSuccess, Is.False);
                      Assert.That(issued.RefusalMessageKey(), Is.EqualTo("OwnerNotFound"));
                    });
  }

  [Test]
  public async Task CreateAsync_NobodyNamed_IssuesAnInvitationWithoutAnOwner()
  {
    ErrorOr<IssuedEnrolmentInvitation> issued = await _service.CreateAsync(null, null, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(issued.IsSuccess, Is.True);
                      Assert.That(issued.Value.Owner, Is.Null);
                      Assert.That(issued.Value.QRCodeValue, Is.EqualTo("ABCDEF"));
                    });
  }

  [Test]
  public async Task CreateAsync_AStationThatAlreadyHoldsATablet_RevokesThatTabletsToken()
  {
    Station kitchen = new()
                      {
                        Id = _stationId,
                        Name = "Kueche",
                        SortOrder = 1,
                        IsActive = true,
                        DeviceId = _deviceId
                      };

    A.CallTo(() => _ownerStore.FindAsync(DeviceOwnerKind.Station, _stationId, A<CancellationToken>._)).Returns(Task.FromResult<IDeviceOwner?>(kitchen));

    ErrorOr<IssuedEnrolmentInvitation> issued = await _service.CreateAsync(null, _stationId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(issued.IsSuccess, Is.True);
                      Assert.That(issued.Value.Owner!.Name, Is.EqualTo("Kueche"));
                    });

    A.CallTo(() => _deviceTokenStore.RevokeAsync(_deviceId, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _announcer.AnnounceAsync(_deviceId, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task EnsureStillOpenAsync_AnInvitationNobodyKnows_FailsBecauseTheInvitationIsUnknown()
  {
    ErrorOr<EnrolmentInvitation> stillOpen = await _service.EnsureStillOpenAsync(_invitationId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(stillOpen.IsSuccess, Is.False);
                      Assert.That(stillOpen.RefusalMessageKey(), Is.EqualTo("admin.enrol.qrUnavailable"));
                    });
  }

  [Test]
  public async Task EnsureStillOpenAsync_AnInvitationAPhoneAlreadyUsed_SaysItWasAlreadyUsed()
  {
    A.CallTo(() => _store.FindByIdAsync(_invitationId, A<CancellationToken>._)).Returns(Task.FromResult<EnrolmentInvitation?>(BuildInvitation(_now.AddMinutes(-1), _deviceId, _now.AddMinutes(4))));

    ErrorOr<EnrolmentInvitation> stillOpen = await _service.EnsureStillOpenAsync(_invitationId, CancellationToken.None);

    Assert.That(stillOpen.RefusalMessageKey(), Is.EqualTo("admin.enrol.qrAlreadyUsed"));
  }

  [Test]
  public async Task EnsureStillOpenAsync_AnInvitationANewerOneReplaced_SaysItWasReplaced()
  {
    A.CallTo(() => _store.FindByIdAsync(_invitationId, A<CancellationToken>._)).Returns(Task.FromResult<EnrolmentInvitation?>(BuildInvitation(_now.AddMinutes(-1), null, _now.AddMinutes(4))));

    ErrorOr<EnrolmentInvitation> stillOpen = await _service.EnsureStillOpenAsync(_invitationId, CancellationToken.None);

    Assert.That(stillOpen.RefusalMessageKey(), Is.EqualTo("admin.enrol.qrReplaced"));
  }

  [Test]
  public async Task EnsureStillOpenAsync_AnInvitationWhoseTimeRanOut_SaysItExpired()
  {
    A.CallTo(() => _store.FindByIdAsync(_invitationId, A<CancellationToken>._)).Returns(Task.FromResult<EnrolmentInvitation?>(BuildInvitation(null, null, _now)));

    ErrorOr<EnrolmentInvitation> stillOpen = await _service.EnsureStillOpenAsync(_invitationId, CancellationToken.None);

    Assert.That(stillOpen.RefusalMessageKey(), Is.EqualTo("admin.enrol.expired"));
  }

  [Test]
  public async Task EnsureStillOpenAsync_AnInvitationThatIsStillOutstanding_AnswersWithIt()
  {
    A.CallTo(() => _store.FindByIdAsync(_invitationId, A<CancellationToken>._)).Returns(Task.FromResult<EnrolmentInvitation?>(BuildInvitation(null, null, _now.AddMinutes(4))));

    ErrorOr<EnrolmentInvitation> stillOpen = await _service.EnsureStillOpenAsync(_invitationId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(stillOpen.IsSuccess, Is.True);
                      Assert.That(stillOpen.Value.Id, Is.EqualTo(_invitationId));
                      Assert.That(stillOpen.Value.ExpiresAtUtc, Is.EqualTo(_now.AddMinutes(4)));
                    });
  }

  [Test]
  public async Task RetireHandedOverDeviceAsync_ATokenThatDoesNotVerify_RevokesNothing()
  {
    A.CallTo(() => _deviceTokenStore.VerifyAsync(A<string>._, A<string>._, A<CancellationToken>._)).Returns(Task.FromResult<IDeviceOwner?>(null));

    Guid? retired = await _service.RetireHandedOverDeviceAsync("lookup", "secret", _deviceId, CancellationToken.None);

    Assert.That(retired, Is.Null);

    A.CallTo(() => _deviceTokenStore.RevokeAsync(A<Guid>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task RetireHandedOverDeviceAsync_TheDeviceThatWasJustSetUp_RevokesNothing()
  {
    A.CallTo(() => _deviceTokenStore.VerifyAsync(A<string>._, A<string>._, A<CancellationToken>._)).Returns(Task.FromResult<IDeviceOwner?>(BuildStaffMemberHolding(_deviceId)));

    Guid? retired = await _service.RetireHandedOverDeviceAsync("lookup", "secret", _deviceId, CancellationToken.None);

    Assert.That(retired, Is.Null);
  }

  [Test]
  public async Task RetireHandedOverDeviceAsync_AnotherDeviceTheBrowserStillHeld_SignsThatOneOut()
  {
    var handedOverDeviceId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    A.CallTo(() => _deviceTokenStore.VerifyAsync(A<string>._, A<string>._, A<CancellationToken>._)).Returns(Task.FromResult<IDeviceOwner?>(BuildStaffMemberHolding(handedOverDeviceId)));

    Guid? retired = await _service.RetireHandedOverDeviceAsync("lookup", "secret", _deviceId, CancellationToken.None);

    Assert.That(retired, Is.EqualTo(handedOverDeviceId));

    A.CallTo(() => _deviceTokenStore.RevokeAsync(handedOverDeviceId, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  private EnrolmentInvitation BuildInvitation(DateTime? consumedAtUtc, Guid? consumedByDeviceId, DateTime expiresAtUtc)
  {
    return new()
           {
             Id = _invitationId,
             QRCodeHash = [1],
             QRCodeSalt = [2],
             QRCodeIterations = 1,
             QRCodeAlgorithm = "PBKDF2-HMAC-SHA512",
             CreatedAtUtc = _now.AddMinutes(-5),
             ExpiresAtUtc = expiresAtUtc,
             ConsumedAtUtc = consumedAtUtc,
             ConsumedByDeviceId = consumedByDeviceId
           };
  }

  private StaffMember BuildStaffMemberHolding(Guid deviceId)
  {
    return new()
           {
             Id = _staffMemberId,
             Name = "Anna",
             IsActive = true,
             CreatedAtUtc = _now,
             DeviceId = deviceId,
             Device = BuildDevice(deviceId)
           };
  }

  private Device BuildDevice(Guid deviceId)
  {
    return new()
           {
             Id = deviceId,
             Language = "de",
             TokenHash = [1],
             TokenSalt = [2],
             TokenIterations = 1,
             TokenAlgorithm = "PBKDF2-HMAC-SHA512",
             TokenLookupId = "lookup",
             CreatedAtUtc = _now,
             LastSeenAtUtc = _now
           };
  }
}
