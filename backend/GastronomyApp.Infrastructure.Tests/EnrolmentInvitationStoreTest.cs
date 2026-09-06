using GastronomyApp.Core.Enums;
using GastronomyApp.Infrastructure.Ports;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Security;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests;

public sealed class EnrolmentInvitationStoreTest
{
  [Test]
  public async Task CreateAsync_ThenRedeemAsync_WithReturnedQrCode_RedeemsForTheStaffMemberTheInvitationNames()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var store = CreateStore(fixture, new());

    var created = await store.CreateAsync(StaffMember(seeded), TestContext.CurrentContext.CancellationToken);
    var redemption = await store.RedeemAsync(new(created.QrCodeValue, "Test agent", "de-DE,de;q=0.9"),
                                             TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(redemption.Outcome, Is.EqualTo(EnrolmentRedemptionOutcome.Redeemed));
                      Assert.That(redemption.OwnerKind, Is.EqualTo(DeviceOwnerKind.StaffMember));
                      Assert.That(redemption.Device, Is.Not.Null);
                      Assert.That(redemption.StaffMember!.Name, Is.EqualTo("Anna"));
                      Assert.That(redemption.StaffMember.DeviceId, Is.EqualTo(redemption.Device!.Id));
                      Assert.That(redemption.Station, Is.Null);
                      Assert.That(redemption.Device.Language, Is.EqualTo("de"));
                    });
  }

  [Test]
  public async Task RedeemAsync_InvitationForAStation_IssuesAStationDeviceAndClearsTheInvitationPointer()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var store = CreateStore(fixture, new());

    var created = await store.CreateAsync(Kitchen(seeded), TestContext.CurrentContext.CancellationToken);
    var kitchenBeforeRedemption = await fixture.DbContext.Stations.SingleAsync(station => station.Id == seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);
    var pointedAtTheInvitation = kitchenBeforeRedemption.EnrolmentInvitationId;

    var redemption = await store.RedeemAsync(new(created.QrCodeValue, "Tablet", "de"),
                                             TestContext.CurrentContext.CancellationToken);
    var kitchen = await fixture.DbContext.Stations.SingleAsync(station => station.Id == seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(pointedAtTheInvitation, Is.EqualTo(created.InvitationId));
                      Assert.That(redemption.Outcome, Is.EqualTo(EnrolmentRedemptionOutcome.Redeemed));
                      Assert.That(redemption.OwnerKind, Is.EqualTo(DeviceOwnerKind.Station));
                      Assert.That(redemption.Station!.Id, Is.EqualTo(seeded.KitchenStationId));
                      Assert.That(redemption.StaffMember, Is.Null);
                      Assert.That(kitchen.DeviceId, Is.EqualTo(redemption.Device!.Id));
                      Assert.That(kitchen.EnrolmentInvitationId, Is.Null);
                    });
  }

  [Test]
  public async Task RedeemAsync_InvitationForAStationThatWasSwitchedOff_IsRejectedAsOffTheList()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var store = CreateStore(fixture, new());

    var created = await store.CreateAsync(Kitchen(seeded), TestContext.CurrentContext.CancellationToken);
    var kitchen = await fixture.DbContext.Stations.SingleAsync(station => station.Id == seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);
    kitchen.IsActive = false;
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    var redemption = await store.RedeemAsync(new(created.QrCodeValue, "Tablet", "de"),
                                             TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(redemption.Outcome, Is.EqualTo(EnrolmentRedemptionOutcome.StationIsOffTheList));
                      Assert.That(redemption.PlaintextToken, Is.Null);
                    });
  }

  [Test]
  public async Task RedeemAsync_InvitationForAStaffMemberWhoWasTakenOffTheList_IsRejected()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var store = CreateStore(fixture, new());

    var created = await store.CreateAsync(StaffMember(seeded), TestContext.CurrentContext.CancellationToken);
    var anna = await fixture.DbContext.StaffMembers.SingleAsync(staffMember => staffMember.Id == seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);
    anna.IsActive = false;
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    var redemption = await store.RedeemAsync(new(created.QrCodeValue, "Phone", "de"),
                                             TestContext.CurrentContext.CancellationToken);

    Assert.That(redemption.Outcome, Is.EqualTo(EnrolmentRedemptionOutcome.StaffMemberIsOffTheList));
  }

  [Test]
  public async Task CreateAsync_Twice_ConsumesTheFirstOutstandingInvitation()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var store = CreateStore(fixture, new());

    var first = await store.CreateAsync(StaffMember(seeded), TestContext.CurrentContext.CancellationToken);
    var second = await store.CreateAsync(StaffMember(seeded), TestContext.CurrentContext.CancellationToken);

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
  public async Task CreateAsync_ANewInvitationForAnotherOwner_TakesTheReplacedInvitationAwayFromTheFirstOwner()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var store = CreateStore(fixture, new());

    var forAnna = await store.CreateAsync(StaffMember(seeded), TestContext.CurrentContext.CancellationToken);
    var forTheKitchen = await store.CreateAsync(Kitchen(seeded), TestContext.CurrentContext.CancellationToken);

    var anna = await fixture.DbContext.StaffMembers.SingleAsync(staffMember => staffMember.Id == seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);
    var kitchen = await fixture.DbContext.Stations.SingleAsync(station => station.Id == seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(forAnna.InvitationId, Is.Not.EqualTo(forTheKitchen.InvitationId));
                      Assert.That(anna.EnrolmentInvitationId, Is.Null);
                      Assert.That(kitchen.EnrolmentInvitationId, Is.EqualTo(forTheKitchen.InvitationId));
                    });
  }

  [Test]
  public async Task RedeemAsync_ExpiredInvitation_IsRejected()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    AdjustableClock clock = new();
    var store = CreateStore(fixture, clock);

    var created = await store.CreateAsync(StaffMember(seeded), TestContext.CurrentContext.CancellationToken);
    clock.Advance(TimeSpan.FromMinutes(6));

    var redemption = await store.RedeemAsync(new(created.QrCodeValue, "Test agent", "de"),
                                             TestContext.CurrentContext.CancellationToken);

    Assert.That(redemption.Outcome, Is.EqualTo(EnrolmentRedemptionOutcome.CodeExpired));
  }

  [Test]
  public async Task RedeemAsync_StaffMemberWithAnEarlierPhone_LeavesOnlyTheNewDevice()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    AdjustableClock clock = new();
    var store = CreateStore(fixture, clock);

    var firstInvitation = await store.CreateAsync(StaffMember(seeded), TestContext.CurrentContext.CancellationToken);
    var firstRedemption = await store.RedeemAsync(new(firstInvitation.QrCodeValue, "Old phone", "de"),
                                                  TestContext.CurrentContext.CancellationToken);

    var staffMemberId = firstRedemption.StaffMember!.Id;
    var oldDeviceId = firstRedemption.Device!.Id;

    var secondInvitation = await store.CreateAsync(StaffMember(seeded), TestContext.CurrentContext.CancellationToken);
    var secondRedemption = await store.RedeemAsync(new(secondInvitation.QrCodeValue, "New phone", "de"),
                                                   TestContext.CurrentContext.CancellationToken);

    var staffMember = await fixture.DbContext.StaffMembers
                                   .SingleAsync(candidate => candidate.Id == staffMemberId, TestContext.CurrentContext.CancellationToken);
    var deviceCount = await fixture.DbContext.Devices.CountAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(secondRedemption.Outcome, Is.EqualTo(EnrolmentRedemptionOutcome.Redeemed));
                      Assert.That(secondRedemption.StaffMember!.Id, Is.EqualTo(staffMemberId));
                      Assert.That(deviceCount, Is.EqualTo(1));
                      Assert.That(staffMember.DeviceId, Is.Not.EqualTo(oldDeviceId));
                      Assert.That(staffMember.DeviceId, Is.EqualTo(secondRedemption.Device!.Id));
                    });
  }

  [Test]
  public async Task CreateAsync_TwoConcurrentCallers_LeaveExactlyOneOutstandingInvitation()
  {
    using SqliteTempFileFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.CreateContext(), TestContext.CurrentContext.CancellationToken);
    AdjustableClock clock = new();

    var firstStore = CreateStore(fixture.CreateContext(), clock);
    var secondStore = CreateStore(fixture.CreateContext(), clock);

    await Task.WhenAll(Task.Run(() => firstStore.CreateAsync(StaffMember(seeded), TestContext.CurrentContext.CancellationToken)),
                       Task.Run(() => secondStore.CreateAsync(Kitchen(seeded), TestContext.CurrentContext.CancellationToken)));

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
    var seeded = await new DomainSeeder().SeedAsync(fixture.CreateContext(), TestContext.CurrentContext.CancellationToken);
    AdjustableClock clock = new();

    var created = await CreateStore(fixture.CreateContext(), clock)
                   .CreateAsync(StaffMember(seeded), TestContext.CurrentContext.CancellationToken);

    var firstStore = CreateStore(fixture.CreateContext(), clock);
    var secondStore = CreateStore(fixture.CreateContext(), clock);

    EnrolmentRedemptionResult[] results = await Task.WhenAll(Task.Run(() => firstStore.RedeemAsync(new(created.QrCodeValue, "First phone", "de"),
                                                                                                   TestContext.CurrentContext.CancellationToken)),
                                                             Task.Run(() => secondStore.RedeemAsync(new(created.QrCodeValue, "Second phone", "de"),
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
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    AdjustableClock clock = new();
    Pbkdf2SecretHasher secretHasher = new();
    DeviceOwnerStore ownerStore = new(fixture.DbContext);
    DeviceTokenStore deviceTokenStore = new(fixture.DbContext, ownerStore, secretHasher, clock);
    EnrolmentInvitationStore store = new(fixture.DbContext, ownerStore, secretHasher, deviceTokenStore, clock);

    var created = await store.CreateAsync(StaffMember(seeded), TestContext.CurrentContext.CancellationToken);
    var redemption = await store.RedeemAsync(new(created.QrCodeValue, "Test agent", "de"),
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
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var store = CreateStore(fixture, new());

    await store.CreateAsync(StaffMember(seeded), TestContext.CurrentContext.CancellationToken);
    var redemption = await store.RedeemAsync(new("not-the-right-code", "Test agent", "de"),
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
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    AdjustableClock clock = new();
    var store = CreateStore(fixture, clock);

    var created = await store.CreateAsync(StaffMember(seeded), TestContext.CurrentContext.CancellationToken);
    clock.Advance(TimeSpan.FromMinutes(6));

    var redemption = await store.RedeemAsync(new(created.QrCodeValue, "Test agent", "de"),
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
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    AdjustableClock clock = new();
    var store = CreateStore(fixture, clock);

    var yesterday =
      await store.CreateAsync(StaffMember(seeded), TestContext.CurrentContext.CancellationToken);

    clock.Advance(TimeSpan.FromDays(1));

    var today =
      await store.CreateAsync(StaffMember(seeded), TestContext.CurrentContext.CancellationToken);

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

  private DeviceOwner StaffMember(SeededDomain seeded)
  {
    return new(DeviceOwnerKind.StaffMember, seeded.StaffMemberId);
  }

  private DeviceOwner Kitchen(SeededDomain seeded)
  {
    return new(DeviceOwnerKind.Station, seeded.KitchenStationId);
  }

  private EnrolmentInvitationStore CreateStore(SqliteInMemoryFixture fixture, AdjustableClock clock)
  {
    return CreateStore(fixture.DbContext, clock);
  }

  private EnrolmentInvitationStore CreateStore(GastronomyAppDbContext dbContext, AdjustableClock clock)
  {
    Pbkdf2SecretHasher secretHasher = new();
    DeviceOwnerStore ownerStore = new(dbContext);

    return new(dbContext,
               ownerStore,
               secretHasher,
               new DeviceTokenStore(dbContext, ownerStore, secretHasher, clock),
               clock);
  }
}
