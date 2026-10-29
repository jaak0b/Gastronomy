using FakeItEasy;
using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace GastronomyApp.Infrastructure.Tests.Services;

[TestFixture]
public sealed class DeviceOwnerRetirementTest
{
  [SetUp]
  public async Task SetUp()
  {
    _fixture = new();
    _seeded = await new DomainSeeder().SeedAsync(_fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    _clock = new(_now);
    _composition = new(_fixture.DbContext, _clock);
  }

  [TearDown]
  public void TearDown()
  {
    _fixture.Dispose();
  }

  private readonly DateTimeOffset _now = new(2026, 8, 27, 18, 0, 0, TimeSpan.Zero);

  private FakeTimeProvider _clock = null!;
  private EnrolmentComposition _composition = null!;
  private SqliteInMemoryFixture _fixture = null!;
  private SeededDomain _seeded = null!;

  [Test]
  public async Task WithdrawOutstandingInvitationAsync_AnOwnerWithNoInvitation_TouchesNothing()
  {
    await _composition.Retirement.WithdrawOutstandingInvitationAsync(null, TestContext.CurrentContext.CancellationToken);

    Assert.That(_fixture.DbContext.ChangeTracker.HasChanges(), Is.False);
  }

  [Test]
  public async Task WithdrawOutstandingInvitationAsync_AnInvitationStillOutstanding_ConsumesItAtTheCurrentMoment()
  {
    var invitationId = await AddInvitationAsync(null);

    await _composition.Retirement.WithdrawOutstandingInvitationAsync(invitationId, TestContext.CurrentContext.CancellationToken);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var invitation = await _fixture.DbContext.EnrolmentInvitations.SingleAsync(candidate => candidate.Id == invitationId, TestContext.CurrentContext.CancellationToken);

    Assert.That(invitation.ConsumedAtUtc, Is.EqualTo(_now.UtcDateTime));
  }

  [Test]
  public async Task WithdrawOutstandingInvitationAsync_AnInvitationAlreadyConsumed_KeepsItsEarlierMoment()
  {
    var earlier = _now.UtcDateTime.AddMinutes(-3);
    var invitationId = await AddInvitationAsync(earlier);

    await _composition.Retirement.WithdrawOutstandingInvitationAsync(invitationId, TestContext.CurrentContext.CancellationToken);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var invitation = await _fixture.DbContext.EnrolmentInvitations.SingleAsync(candidate => candidate.Id == invitationId, TestContext.CurrentContext.CancellationToken);

    Assert.That(invitation.ConsumedAtUtc, Is.EqualTo(earlier));
  }

  [Test]
  public async Task RevokeDeviceAsync_AnOwnerWithoutADevice_AnswersWithNothingRevoked()
  {
    Assert.That(await _composition.Retirement.RevokeDeviceAsync(null, TestContext.CurrentContext.CancellationToken), Is.Null);

    A.CallTo(() => _composition.RevocationAnnouncer.AnnounceDeviceRevokedAsync(A<Guid>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task RevokeDeviceAsync_AnOwnerHoldingADevice_RemovesItClearsTheOwnersPointerAndNamesIt()
  {
    var deviceId = await GiveAnnaADeviceAsync();

    var revoked = await _composition.Retirement.RevokeDeviceAsync(deviceId, TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var deviceStillStored = await _fixture.DbContext.Devices.AnyAsync(device => device.Id == deviceId, TestContext.CurrentContext.CancellationToken);
    var anna = await _fixture.DbContext.StaffMembers.SingleAsync(staffMember => staffMember.Id == _seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(revoked, Is.EqualTo(deviceId));
                      Assert.That(deviceStillStored, Is.False);
                      Assert.That(anna.DeviceId, Is.Null);
                    });

    A.CallTo(() => _composition.RevocationAnnouncer.AnnounceDeviceRevokedAsync(deviceId, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task RevokeDeviceAsync_ADeviceThatIsAlreadyGone_StillTellsTheLaptopAndStoresNothing()
  {
    var unknownDeviceId = Guid.NewGuid();

    var revoked = await _composition.Retirement.RevokeDeviceAsync(unknownDeviceId, TestContext.CurrentContext.CancellationToken);

    Assert.That(revoked, Is.EqualTo(unknownDeviceId));

    A.CallTo(() => _composition.RevocationAnnouncer.AnnounceDeviceRevokedAsync(unknownDeviceId, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  private async Task<Guid> AddInvitationAsync(DateTime? consumedAtUtc)
  {
    EnrolmentInvitation invitation = new()
                                     {
                                       Id = Guid.NewGuid(),
                                       QRCodeHash = [1],
                                       QRCodeSalt = [2],
                                       QRCodeAlgorithm = "SHA-256",
                                       CreatedAtUtc = _now.UtcDateTime,
                                       ExpiresAtUtc = _now.UtcDateTime.AddMinutes(5),
                                       ConsumedAtUtc = consumedAtUtc
                                     };

    _fixture.DbContext.EnrolmentInvitations.Add(invitation);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    return invitation.Id;
  }

  private async Task<Guid> GiveAnnaADeviceAsync()
  {
    Device device = new()
                    {
                      Id = Guid.NewGuid(),
                      Language = "de",
                      TokenHash = [1],
                      TokenSalt = [2],
                      TokenAlgorithm = "SHA-256",
                      TokenLookupId = Guid.NewGuid().ToString("N"),
                      CreatedAtUtc = _now.UtcDateTime
                    };

    _fixture.DbContext.Devices.Add(device);
    var anna = await _fixture.DbContext.StaffMembers.SingleAsync(staffMember => staffMember.Id == _seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);
    anna.DeviceId = device.Id;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    return device.Id;
  }
}
