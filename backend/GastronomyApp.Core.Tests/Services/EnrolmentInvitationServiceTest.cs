using FakeItEasy;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;

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
    _clock = A.Fake<IClock>();

    A.CallTo(() => _clock.UtcNow).Returns(_now);
    A.CallTo(() => _store.CreateAsync(A<DeviceOwner?>._, A<CancellationToken>._)).Returns(new EnrolmentInvitationCreated(_invitationId, "ABCDEF", _now.AddMinutes(5)));
    A.CallTo(() => _ownerStore.FindAsync(A<DeviceOwner>._!, A<CancellationToken>._)).Returns(Task.FromResult<DeviceOwnerRecord?>(null));
    A.CallTo(() => _store.FindByIdAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<EnrolmentInvitation?>(null));

    _service = new(_store, _ownerStore, _deviceTokenStore, new(_store, _deviceTokenStore, _clock), _clock);
  }

  private readonly DateTime _now = new(2026, 8, 27, 18, 0, 0, DateTimeKind.Utc);
  private readonly Guid _deviceId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
  private readonly Guid _invitationId = Guid.Parse("ffffffff-0000-0000-0000-000000000001");
  private readonly Guid _staffMemberId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
  private readonly Guid _stationId = Guid.Parse("dddddddd-0000-0000-0000-000000000001");

  private IClock _clock = null!;
  private IDeviceTokenStore _deviceTokenStore = null!;
  private IDeviceOwnerStore _ownerStore = null!;
  private EnrolmentInvitationService _service = null!;
  private IEnrolmentInvitationStore _store = null!;

  [Test]
  public void RedeemAsync_NullRequest_ThrowsArgumentNullException()
  {
    Assert.That(async () => await _service.RedeemAsync(null!, CancellationToken.None), Throws.ArgumentNullException);
  }

  [Test]
  public async Task CreateAsync_BothAWaiterAndAStation_FailsBecauseOnlyOneOwnerIsAllowed()
  {
    Result<IssuedEnrolmentInvitation, Failure<EnrolmentInvitationFailureReason>> issued = await _service.CreateAsync(_staffMemberId, _stationId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(issued.IsSuccess, Is.False);
                      Assert.That(issued.Failure.Reason, Is.EqualTo(EnrolmentInvitationFailureReason.AtMostOneOwner));
                    });

    A.CallTo(() => _store.CreateAsync(A<DeviceOwner?>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task CreateAsync_AnOwnerWhoIsNotOnTheList_FailsBecauseTheOwnerIsNotFound()
  {
    Result<IssuedEnrolmentInvitation, Failure<EnrolmentInvitationFailureReason>> issued = await _service.CreateAsync(_staffMemberId, null, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(issued.IsSuccess, Is.False);
                      Assert.That(issued.Failure.Reason, Is.EqualTo(EnrolmentInvitationFailureReason.OwnerNotFound));
                    });
  }

  [Test]
  public async Task CreateAsync_NobodyNamed_IssuesAnInvitationWithoutAnOwner()
  {
    Result<IssuedEnrolmentInvitation, Failure<EnrolmentInvitationFailureReason>> issued = await _service.CreateAsync(null, null, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(issued.IsSuccess, Is.True);
                      Assert.That(issued.Value.Owner, Is.Null);
                      Assert.That(issued.Value.QRCodeValue, Is.EqualTo("ABCDEF"));
                      Assert.That(issued.Value.RevokedDeviceId, Is.Null);
                    });
  }

  [Test]
  public async Task CreateAsync_AStationThatAlreadyHoldsATablet_RevokesThatTabletsToken()
  {
    A.CallTo(() => _ownerStore.FindAsync(A<DeviceOwner>.That.Matches(owner => owner.Id == _stationId), A<CancellationToken>._))
   .Returns(Task.FromResult<DeviceOwnerRecord?>(new()
                                                {
                                                  Owner = new(DeviceOwnerKind.Station, _stationId),
                                                  Name = "Kueche",
                                                  IsActive = true,
                                                  DeviceId = _deviceId,
                                                  EnrolmentInvitationId = null
                                                }));

    Result<IssuedEnrolmentInvitation, Failure<EnrolmentInvitationFailureReason>> issued = await _service.CreateAsync(null, _stationId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(issued.IsSuccess, Is.True);
                      Assert.That(issued.Value.OwnerName, Is.EqualTo("Kueche"));
                      Assert.That(issued.Value.RevokedDeviceId, Is.EqualTo(_deviceId));
                    });

    A.CallTo(() => _deviceTokenStore.RevokeAsync(_deviceId, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task EnsureStillOpenAsync_AnInvitationNobodyKnows_FailsBecauseTheInvitationIsUnknown()
  {
    Result<OpenEnrolmentInvitation, Failure<EnrolmentInvitationFailureReason>> stillOpen = await _service.EnsureStillOpenAsync(_invitationId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(stillOpen.IsSuccess, Is.False);
                      Assert.That(stillOpen.Failure.Reason, Is.EqualTo(EnrolmentInvitationFailureReason.InvitationUnknown));
                    });
  }

  [Test]
  public async Task EnsureStillOpenAsync_AnInvitationAPhoneAlreadyUsed_SaysItWasAlreadyUsed()
  {
    A.CallTo(() => _store.FindByIdAsync(_invitationId, A<CancellationToken>._)).Returns(Task.FromResult<EnrolmentInvitation?>(BuildInvitation(_now.AddMinutes(-1), _deviceId, _now.AddMinutes(4))));

    Result<OpenEnrolmentInvitation, Failure<EnrolmentInvitationFailureReason>> stillOpen = await _service.EnsureStillOpenAsync(_invitationId, CancellationToken.None);

    Assert.That(stillOpen.Failure.Reason, Is.EqualTo(EnrolmentInvitationFailureReason.InvitationAlreadyUsed));
  }

  [Test]
  public async Task EnsureStillOpenAsync_AnInvitationANewerOneReplaced_SaysItWasReplaced()
  {
    A.CallTo(() => _store.FindByIdAsync(_invitationId, A<CancellationToken>._)).Returns(Task.FromResult<EnrolmentInvitation?>(BuildInvitation(_now.AddMinutes(-1), null, _now.AddMinutes(4))));

    Result<OpenEnrolmentInvitation, Failure<EnrolmentInvitationFailureReason>> stillOpen = await _service.EnsureStillOpenAsync(_invitationId, CancellationToken.None);

    Assert.That(stillOpen.Failure.Reason, Is.EqualTo(EnrolmentInvitationFailureReason.InvitationReplaced));
  }

  [Test]
  public async Task EnsureStillOpenAsync_AnInvitationWhoseTimeRanOut_SaysItExpired()
  {
    A.CallTo(() => _store.FindByIdAsync(_invitationId, A<CancellationToken>._)).Returns(Task.FromResult<EnrolmentInvitation?>(BuildInvitation(null, null, _now)));

    Result<OpenEnrolmentInvitation, Failure<EnrolmentInvitationFailureReason>> stillOpen = await _service.EnsureStillOpenAsync(_invitationId, CancellationToken.None);

    Assert.That(stillOpen.Failure.Reason, Is.EqualTo(EnrolmentInvitationFailureReason.InvitationExpired));
  }

  [Test]
  public async Task EnsureStillOpenAsync_AnInvitationThatIsStillOutstanding_AnswersWithIt()
  {
    A.CallTo(() => _store.FindByIdAsync(_invitationId, A<CancellationToken>._)).Returns(Task.FromResult<EnrolmentInvitation?>(BuildInvitation(null, null, _now.AddMinutes(4))));

    Result<OpenEnrolmentInvitation, Failure<EnrolmentInvitationFailureReason>> stillOpen = await _service.EnsureStillOpenAsync(_invitationId, CancellationToken.None);

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
    A.CallTo(() => _deviceTokenStore.VerifyAsync(A<string>._, A<string>._, A<CancellationToken>._)).Returns(new DeviceVerificationResult(false, null, null));

    Guid? retired = await _service.RetireHandedOverDeviceAsync("lookup", "secret", _deviceId, CancellationToken.None);

    Assert.That(retired, Is.Null);

    A.CallTo(() => _deviceTokenStore.RevokeAsync(A<Guid>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task RetireHandedOverDeviceAsync_TheDeviceThatWasJustSetUp_RevokesNothing()
  {
    A.CallTo(() => _deviceTokenStore.VerifyAsync(A<string>._, A<string>._, A<CancellationToken>._)).Returns(new DeviceVerificationResult(true, BuildDevice(_deviceId), null));

    Guid? retired = await _service.RetireHandedOverDeviceAsync("lookup", "secret", _deviceId, CancellationToken.None);

    Assert.That(retired, Is.Null);
  }

  [Test]
  public async Task RetireHandedOverDeviceAsync_AnotherDeviceTheBrowserStillHeld_SignsThatOneOut()
  {
    var handedOverDeviceId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    A.CallTo(() => _deviceTokenStore.VerifyAsync(A<string>._, A<string>._, A<CancellationToken>._)).Returns(new DeviceVerificationResult(true, BuildDevice(handedOverDeviceId), null));

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
