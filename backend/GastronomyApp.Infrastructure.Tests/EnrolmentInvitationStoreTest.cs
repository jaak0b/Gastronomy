using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure.Ports;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Security;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests;

public sealed class EnrolmentInvitationStoreTest
{
  [Test]
  public async Task CreateAsync_ThenRedeemAsync_WithReturnedQrCode_Redeems()
  {
    using SqliteInMemoryFixture fixture = new();
    var store = CreateStore(fixture, new());

    var created = await store.CreateAsync(null, TestContext.CurrentContext.CancellationToken);
    var redemption = await store.RedeemAsync(new(created.QrCodeValue, "Anna", "Test agent", "de-DE,de;q=0.9"),
                                             TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(redemption.Outcome, Is.EqualTo(EnrolmentRedemptionOutcome.Redeemed));
                      Assert.That(redemption.Device, Is.Not.Null);
                      Assert.That(redemption.StaffMember!.Name, Is.EqualTo("Anna"));
                      Assert.That(redemption.Device!.Language, Is.EqualTo("de"));
                    });
  }

  [Test]
  public async Task CreateAsync_Twice_ConsumesTheFirstOutstandingInvitation()
  {
    using SqliteInMemoryFixture fixture = new();
    var store = CreateStore(fixture, new());

    var first = await store.CreateAsync(null, TestContext.CurrentContext.CancellationToken);
    var second = await store.CreateAsync(null, TestContext.CurrentContext.CancellationToken);

    var firstRow = await fixture.DbContext.EnrolmentInvitations
                                .SingleAsync(invitation => invitation.Id == first.InvitationId, TestContext.CurrentContext.CancellationToken);
    var secondRow = await fixture.DbContext.EnrolmentInvitations
                                 .SingleAsync(invitation => invitation.Id == second.InvitationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(firstRow.ConsumedAtUtc, Is.Not.Null);
                      Assert.That(secondRow.ConsumedAtUtc, Is.Null);
                    });
  }

  [Test]
  public async Task RedeemAsync_ExpiredInvitation_IsRejected()
  {
    using SqliteInMemoryFixture fixture = new();
    AdjustableClock clock = new();
    var store = CreateStore(fixture, clock);

    var created = await store.CreateAsync(null, TestContext.CurrentContext.CancellationToken);
    clock.Advance(TimeSpan.FromMinutes(6));

    var redemption = await store.RedeemAsync(new(created.QrCodeValue, "Anna", "Test agent", "de"),
                                             TestContext.CurrentContext.CancellationToken);

    Assert.That(redemption.Outcome, Is.EqualTo(EnrolmentRedemptionOutcome.CodeExpired));
  }

  [Test]
  public async Task RedeemAsync_StaffMemberWithAnEarlierPhone_LeavesOnlyTheNewDevice()
  {
    using SqliteInMemoryFixture fixture = new();
    AdjustableClock clock = new();
    var store = CreateStore(fixture, clock);

    var firstInvitation = await store.CreateAsync(null, TestContext.CurrentContext.CancellationToken);
    var firstRedemption = await store.RedeemAsync(new(firstInvitation.QrCodeValue, "Anna", "Old phone", "de"),
                                                  TestContext.CurrentContext.CancellationToken);

    var staffMemberId = firstRedemption.StaffMember!.Id;
    var oldDeviceId = firstRedemption.Device!.Id;

    var secondInvitation = await store.CreateAsync(staffMemberId, TestContext.CurrentContext.CancellationToken);
    var secondRedemption = await store.RedeemAsync(new(secondInvitation.QrCodeValue, "Anna", "New phone", "de"),
                                                   TestContext.CurrentContext.CancellationToken);

    List<Device> devices = await fixture.DbContext.Devices
                                        .Where(device => device.StaffMemberId == staffMemberId)
                                        .ToListAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(secondRedemption.Outcome, Is.EqualTo(EnrolmentRedemptionOutcome.Redeemed));
                      Assert.That(secondRedemption.StaffMember!.Id, Is.EqualTo(staffMemberId));
                      Assert.That(devices, Has.Count.EqualTo(1));
                      Assert.That(devices[0].Id, Is.Not.EqualTo(oldDeviceId));
                    });
  }

  [Test]
  public async Task CreateAsync_TwoConcurrentCallers_LeaveExactlyOneOutstandingInvitation()
  {
    using SqliteTempFileFixture fixture = new();
    AdjustableClock clock = new();

    var firstStore = CreateStore(fixture.CreateContext(), clock);
    var secondStore = CreateStore(fixture.CreateContext(), clock);

    await Task.WhenAll(Task.Run(() => firstStore.CreateAsync(null, TestContext.CurrentContext.CancellationToken)),
                       Task.Run(() => secondStore.CreateAsync(null, TestContext.CurrentContext.CancellationToken)));

    var verificationContext = fixture.CreateContext();
    var outstandingCount = await verificationContext.EnrolmentInvitations
                                                    .CountAsync(invitation => invitation.ConsumedAtUtc == null && invitation.ExpiresAtUtc > clock.UtcNow,
                                                                TestContext.CurrentContext.CancellationToken);

    Assert.That(outstandingCount, Is.EqualTo(1));
  }

  [Test]
  public async Task RedeemAsync_TwoConcurrentCallers_IssueExactlyOneDevice()
  {
    using SqliteTempFileFixture fixture = new();
    AdjustableClock clock = new();

    var created = await CreateStore(fixture.CreateContext(), clock)
                   .CreateAsync(null, TestContext.CurrentContext.CancellationToken);

    var firstStore = CreateStore(fixture.CreateContext(), clock);
    var secondStore = CreateStore(fixture.CreateContext(), clock);

    EnrolmentRedemptionResult[] results = await Task.WhenAll(Task.Run(() => firstStore.RedeemAsync(new(created.QrCodeValue, "Anna", "First phone", "de"),
                                                                                                   TestContext.CurrentContext.CancellationToken)),
                                                             Task.Run(() => secondStore.RedeemAsync(new(created.QrCodeValue, "Anna", "Second phone", "de"),
                                                                                                    TestContext.CurrentContext.CancellationToken)));

    var verificationContext = fixture.CreateContext();
    var deviceCount = await verificationContext.Devices
                                               .CountAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(results.Count(result => result.Outcome == EnrolmentRedemptionOutcome.Redeemed),
                                  Is.EqualTo(1));
                      Assert.That(deviceCount, Is.EqualTo(1));
                    });
  }

  [Test]
  public async Task RedeemAsync_SuccessfulRedemption_CarriesAPlaintextTokenThatVerifies()
  {
    using SqliteInMemoryFixture fixture = new();
    AdjustableClock clock = new();
    Pbkdf2SecretHasher secretHasher = new();
    DeviceTokenStore deviceTokenStore = new(fixture.DbContext, secretHasher, clock);
    EnrolmentInvitationStore store = new(fixture.DbContext, secretHasher, deviceTokenStore, clock);

    var created = await store.CreateAsync(null, TestContext.CurrentContext.CancellationToken);
    var redemption = await store.RedeemAsync(new(created.QrCodeValue, "Anna", "Test agent", "de"),
                                             TestContext.CurrentContext.CancellationToken);

    Assert.That(redemption.PlaintextToken, Is.Not.Null);

    var separatorIndex = redemption.PlaintextToken!.IndexOf('.', StringComparison.Ordinal);
    var verification = await deviceTokenStore.VerifyAsync(redemption.PlaintextToken[..separatorIndex],
                                                          redemption.PlaintextToken[(separatorIndex + 1)..],
                                                          TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(verification.IsValid, Is.True);
                      Assert.That(verification.Device!.Id, Is.EqualTo(redemption.Device!.Id));
                    });
  }

  [Test]
  public async Task RedeemAsync_WrongQrCode_CarriesNoPlaintextToken()
  {
    using SqliteInMemoryFixture fixture = new();
    var store = CreateStore(fixture, new());

    await store.CreateAsync(null, TestContext.CurrentContext.CancellationToken);
    var redemption = await store.RedeemAsync(new("not-the-right-code", "Anna", "Test agent", "de"),
                                             TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(redemption.Outcome, Is.EqualTo(EnrolmentRedemptionOutcome.CodeInvalid));
                      Assert.That(redemption.PlaintextToken, Is.Null);
                    });
  }

  [Test]
  public async Task RedeemAsync_ExpiredInvitation_CarriesNoPlaintextToken()
  {
    using SqliteInMemoryFixture fixture = new();
    AdjustableClock clock = new();
    var store = CreateStore(fixture, clock);

    var created = await store.CreateAsync(null, TestContext.CurrentContext.CancellationToken);
    clock.Advance(TimeSpan.FromMinutes(6));

    var redemption = await store.RedeemAsync(new(created.QrCodeValue, "Anna", "Test agent", "de"),
                                             TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(redemption.Outcome, Is.EqualTo(EnrolmentRedemptionOutcome.CodeExpired));
                      Assert.That(redemption.PlaintextToken, Is.Null);
                    });
  }

  [Test]
  public async Task CreateAsync_AfterAnEarlierInvitationExpiredUnredeemed_ConsumesItAndSucceeds()
  {
    using SqliteInMemoryFixture fixture = new();
    AdjustableClock clock = new();
    var store = CreateStore(fixture, clock);

    var yesterday =
      await store.CreateAsync(null, TestContext.CurrentContext.CancellationToken);

    clock.Advance(TimeSpan.FromDays(1));

    var today =
      await store.CreateAsync(null, TestContext.CurrentContext.CancellationToken);

    var expiredRow = await fixture.DbContext.EnrolmentInvitations
                                  .SingleAsync(invitation => invitation.Id == yesterday.InvitationId,
                                               TestContext.CurrentContext.CancellationToken);

    var currentRow = await fixture.DbContext.EnrolmentInvitations
                                  .SingleAsync(invitation => invitation.Id == today.InvitationId,
                                               TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(expiredRow.ConsumedAtUtc,
                                  Is.Not.Null,
                                  "An expired invitation still holds the single outstanding marker until it is consumed.");
                      Assert.That(currentRow.ConsumedAtUtc, Is.Null);
                    });
  }

  private EnrolmentInvitationStore CreateStore(SqliteInMemoryFixture fixture, AdjustableClock clock)
  {
    return CreateStore(fixture.DbContext, clock);
  }

  private EnrolmentInvitationStore CreateStore(GastronomyAppDbContext dbContext, AdjustableClock clock)
  {
    Pbkdf2SecretHasher secretHasher = new();

    return new(dbContext,
               secretHasher,
               new DeviceTokenStore(dbContext, secretHasher, clock),
               clock);
  }
}
