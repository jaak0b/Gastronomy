using FakeItEasy;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class DeviceOwnerRetirementTest
{
  [SetUp]
  public void SetUp()
  {
    _invitationStore = A.Fake<IEnrolmentInvitationStore>();
    _deviceTokenStore = A.Fake<IDeviceTokenStore>();
    _clock = A.Fake<IClock>();

    A.CallTo(() => _clock.UtcNow).Returns(_now);

    _retirement = new(_invitationStore, _deviceTokenStore, _clock);
  }

  private readonly DateTime _now = new(2026, 8, 27, 18, 0, 0, DateTimeKind.Utc);
  private readonly Guid _deviceId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
  private readonly Guid _invitationId = Guid.Parse("ffffffff-0000-0000-0000-000000000001");

  private IClock _clock = null!;
  private IDeviceTokenStore _deviceTokenStore = null!;
  private IEnrolmentInvitationStore _invitationStore = null!;
  private DeviceOwnerRetirement _retirement = null!;

  [Test]
  public async Task WithdrawOutstandingInvitationAsync_AnOwnerWithNoInvitation_TouchesNothing()
  {
    await _retirement.WithdrawOutstandingInvitationAsync(null, CancellationToken.None);

    A.CallTo(() => _invitationStore.ConsumeAsync(A<Guid>._, A<DateTime>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task WithdrawOutstandingInvitationAsync_AnInvitationStillOutstanding_ConsumesItAtTheCurrentMoment()
  {
    await _retirement.WithdrawOutstandingInvitationAsync(_invitationId, CancellationToken.None);

    A.CallTo(() => _invitationStore.ConsumeAsync(_invitationId, _now, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task RevokeDeviceAsync_AnOwnerWithoutADevice_AnswersWithNothingRevoked()
  {
    Assert.That(await _retirement.RevokeDeviceAsync(null, CancellationToken.None), Is.Null);

    A.CallTo(() => _deviceTokenStore.RevokeAsync(A<Guid>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task RevokeDeviceAsync_AnOwnerHoldingADevice_RevokesItAndNamesIt()
  {
    Assert.That(await _retirement.RevokeDeviceAsync(_deviceId, CancellationToken.None), Is.EqualTo(_deviceId));

    A.CallTo(() => _deviceTokenStore.RevokeAsync(_deviceId, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }
}
