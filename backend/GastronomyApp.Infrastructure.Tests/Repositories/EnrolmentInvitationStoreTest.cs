using ErrorOr;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;
using GastronomyApp.Infrastructure.Persistence;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Security;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace GastronomyApp.Infrastructure.Tests.Repositories;

public sealed class EnrolmentInvitationStoreTest
{
  [Test]
  public async Task CreateAsync_ThenRedeemAsync_WithReturnedQrCode_RedeemsForTheStaffMemberTheInvitationNames()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var store = CreateStore(fixture, new());

    var created = await store.CreateAsync(await AnnaAsync(fixture.DbContext, seeded), TestContext.CurrentContext.CancellationToken);
    var redemption = await store.RedeemAsync(created.QRCodeValue, null, "Test agent", "de-DE,de;q=0.9", TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(redemption.IsSuccess, Is.True);
                      Assert.That(redemption.Value.Owner.Kind, Is.EqualTo(DeviceOwnerKind.StaffMember));
                      Assert.That(redemption.Value.Owner.Device, Is.Not.Null);
                      Assert.That(redemption.Value.Owner.Name, Is.EqualTo("Anna"));
                      Assert.That(redemption.Value.Owner.DeviceId, Is.EqualTo(redemption.Value.Owner.Device!.Id));
                      Assert.That(redemption.Value.Owner.Device.Language, Is.EqualTo("de"));
                    });
  }

  [Test]
  public async Task CreateAsync_ForNobody_ThenRedeemAsync_WithATypedName_RedeemsForANewStaffMember()
  {
    using SqliteInMemoryFixture fixture = new();
    var store = CreateStore(fixture, new());

    var created = await store.CreateAsync(null, TestContext.CurrentContext.CancellationToken);
    var redemption = await store.RedeemAsync(created.QRCodeValue, "Bernd", "Test agent", "de-DE,de;q=0.9", TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(redemption.IsSuccess, Is.True);
                      Assert.That(redemption.Value.Owner.Kind, Is.EqualTo(DeviceOwnerKind.StaffMember));
                      Assert.That(redemption.Value.Owner.Device, Is.Not.Null);
                      Assert.That(redemption.Value.Owner.Name, Is.EqualTo("Bernd"));
                      Assert.That(redemption.Value.Owner.IsActive, Is.True);
                      Assert.That(redemption.Value.Owner.DeviceId, Is.EqualTo(redemption.Value.Owner.Device!.Id));
                    });
  }

  [Test]
  public async Task RedeemAsync_InvitationForNobodyAndABlankName_AsksForTheName()
  {
    using SqliteInMemoryFixture fixture = new();
    var store = CreateStore(fixture, new());

    var created = await store.CreateAsync(null, TestContext.CurrentContext.CancellationToken);
    var redemption = await store.RedeemAsync(created.QRCodeValue, "   ", "Test agent", "de", TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(redemption.RefusalMessageKey(), Is.EqualTo("enrolment.nameMissing"));
                    });
  }

  [Test]
  public async Task RedeemAsync_InvitationForAStation_IssuesAStationDeviceAndClearsTheInvitationPointer()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var store = CreateStore(fixture, new());

    var created = await store.CreateAsync(await KitchenAsync(fixture.DbContext, seeded), TestContext.CurrentContext.CancellationToken);
    var kitchenBeforeRedemption = await fixture.DbContext.Stations.SingleAsync(station => station.Id == seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);
    Guid? pointedAtTheInvitation = kitchenBeforeRedemption.EnrolmentInvitationId;

    var redemption = await store.RedeemAsync(created.QRCodeValue, null, "Tablet", "de", TestContext.CurrentContext.CancellationToken);
    var kitchen = await fixture.DbContext.Stations.SingleAsync(station => station.Id == seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(pointedAtTheInvitation, Is.EqualTo(created.Invitation.Id));
                      Assert.That(redemption.IsSuccess, Is.True);
                      Assert.That(redemption.Value.Owner.Kind, Is.EqualTo(DeviceOwnerKind.Station));
                      Assert.That(redemption.Value.Owner.Id, Is.EqualTo(seeded.KitchenStationId));
                      Assert.That(kitchen.DeviceId, Is.EqualTo(redemption.Value.Owner.Device!.Id));
                      Assert.That(kitchen.EnrolmentInvitationId, Is.Null);
                    });
  }

  [Test]
  public async Task RedeemAsync_InvitationForAStationThatWasSwitchedOff_IsRejectedAsOffTheList()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var store = CreateStore(fixture, new());

    var created = await store.CreateAsync(await KitchenAsync(fixture.DbContext, seeded), TestContext.CurrentContext.CancellationToken);
    var kitchen = await fixture.DbContext.Stations.SingleAsync(station => station.Id == seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);
    kitchen.IsActive = false;
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    var redemption = await store.RedeemAsync(created.QRCodeValue, null, "Tablet", "de", TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(redemption.RefusalMessageKey(), Is.EqualTo("enrolment.stationIsOffTheList"));
                    });
  }

  [Test]
  public async Task RedeemAsync_InvitationForAStaffMemberWhoWasTakenOffTheList_IsRejected()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var store = CreateStore(fixture, new());

    var created = await store.CreateAsync(await AnnaAsync(fixture.DbContext, seeded), TestContext.CurrentContext.CancellationToken);
    var anna = await fixture.DbContext.StaffMembers.SingleAsync(staffMember => staffMember.Id == seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);
    anna.IsActive = false;
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    var redemption = await store.RedeemAsync(created.QRCodeValue, null, "Phone", "de", TestContext.CurrentContext.CancellationToken);

    Assert.That(redemption.RefusalMessageKey(), Is.EqualTo("enrolment.staffMemberIsOffTheList"));
  }

  [Test]
  public async Task CreateAsync_Twice_ConsumesTheFirstOutstandingInvitation()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var store = CreateStore(fixture, new());

    var first = await store.CreateAsync(await AnnaAsync(fixture.DbContext, seeded), TestContext.CurrentContext.CancellationToken);
    var second = await store.CreateAsync(await AnnaAsync(fixture.DbContext, seeded), TestContext.CurrentContext.CancellationToken);

    var firstRow = await fixture.DbContext.EnrolmentInvitations.SingleAsync(invitation => invitation.Id == first.Invitation.Id, TestContext.CurrentContext.CancellationToken);
    var secondRow = await fixture.DbContext.EnrolmentInvitations.SingleAsync(invitation => invitation.Id == second.Invitation.Id, TestContext.CurrentContext.CancellationToken);

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

    var forAnna = await store.CreateAsync(await AnnaAsync(fixture.DbContext, seeded), TestContext.CurrentContext.CancellationToken);
    var forTheKitchen = await store.CreateAsync(await KitchenAsync(fixture.DbContext, seeded), TestContext.CurrentContext.CancellationToken);

    var anna = await fixture.DbContext.StaffMembers.SingleAsync(staffMember => staffMember.Id == seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);
    var kitchen = await fixture.DbContext.Stations.SingleAsync(station => station.Id == seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(forAnna.Invitation.Id, Is.Not.EqualTo(forTheKitchen.Invitation.Id));
                      Assert.That(anna.EnrolmentInvitationId, Is.Null);
                      Assert.That(kitchen.EnrolmentInvitationId, Is.EqualTo(forTheKitchen.Invitation.Id));
                    });
  }

  [Test]
  public async Task RedeemAsync_ExpiredInvitation_IsRejected()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    FakeTimeProvider clock = new(new(2026, 8, 27, 18, 0, 0, TimeSpan.Zero));
    var store = CreateStore(fixture, clock);

    var created = await store.CreateAsync(await AnnaAsync(fixture.DbContext, seeded), TestContext.CurrentContext.CancellationToken);
    clock.Advance(TimeSpan.FromMinutes(6));

    var redemption = await store.RedeemAsync(created.QRCodeValue, null, "Test agent", "de", TestContext.CurrentContext.CancellationToken);

    Assert.That(redemption.RefusalMessageKey(), Is.EqualTo("enrolment.codeNoLongerValid"));
  }

  [Test]
  public async Task RedeemAsync_StaffMemberWithAnEarlierPhone_LeavesOnlyTheNewDevice()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    FakeTimeProvider clock = new(new(2026, 8, 27, 18, 0, 0, TimeSpan.Zero));
    var store = CreateStore(fixture, clock);

    var firstInvitation = await store.CreateAsync(await AnnaAsync(fixture.DbContext, seeded), TestContext.CurrentContext.CancellationToken);
    var firstRedemption = await store.RedeemAsync(firstInvitation.QRCodeValue, null, "Old phone", "de", TestContext.CurrentContext.CancellationToken);

    var staffMemberId = firstRedemption.Value.Owner.Id;
    var oldDeviceId = firstRedemption.Value.Owner.Device!.Id;

    var secondInvitation = await store.CreateAsync(await AnnaAsync(fixture.DbContext, seeded), TestContext.CurrentContext.CancellationToken);
    var secondRedemption = await store.RedeemAsync(secondInvitation.QRCodeValue, null, "New phone", "de", TestContext.CurrentContext.CancellationToken);

    var staffMember = await fixture.DbContext.StaffMembers.SingleAsync(candidate => candidate.Id == staffMemberId, TestContext.CurrentContext.CancellationToken);
    var deviceCount = await fixture.DbContext.Devices.CountAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(secondRedemption.IsSuccess, Is.True);
                      Assert.That(secondRedemption.Value.Owner.Id, Is.EqualTo(staffMemberId));
                      Assert.That(deviceCount, Is.EqualTo(1));
                      Assert.That(staffMember.DeviceId, Is.Not.EqualTo(oldDeviceId));
                      Assert.That(staffMember.DeviceId, Is.EqualTo(secondRedemption.Value.Owner.Device!.Id));
                    });
  }

  [Test]
  public async Task CreateAsync_TwoConcurrentCallers_LeaveExactlyOneOutstandingInvitation()
  {
    using SqliteTempFileFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.CreateContext(), TestContext.CurrentContext.CancellationToken);
    FakeTimeProvider clock = new(new(2026, 8, 27, 18, 0, 0, TimeSpan.Zero));

    var firstContext = fixture.CreateContext();
    var secondContext = fixture.CreateContext();
    var firstStore = CreateStore(firstContext, clock);
    var secondStore = CreateStore(secondContext, clock);
    var anna = await AnnaAsync(firstContext, seeded);
    var kitchen = await KitchenAsync(secondContext, seeded);

    await Task.WhenAll(Task.Run(() => firstStore.CreateAsync(anna, TestContext.CurrentContext.CancellationToken)), Task.Run(() => secondStore.CreateAsync(kitchen, TestContext.CurrentContext.CancellationToken)));

    var verificationContext = fixture.CreateContext();
    var outstandingCount = await verificationContext.EnrolmentInvitations.CountAsync(invitation => invitation.ConsumedAtUtc == null && invitation.ExpiresAtUtc > clock.GetUtcNow().UtcDateTime, TestContext.CurrentContext.CancellationToken);

    Assert.That(outstandingCount, Is.EqualTo(1));
  }

  [Test]
  public async Task RedeemAsync_TwoConcurrentCallers_IssueExactlyOneDevice()
  {
    using SqliteTempFileFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.CreateContext(), TestContext.CurrentContext.CancellationToken);
    FakeTimeProvider clock = new(new(2026, 8, 27, 18, 0, 0, TimeSpan.Zero));

    var creatingContext = fixture.CreateContext();
    var created = await CreateStore(creatingContext, clock).CreateAsync(await AnnaAsync(creatingContext, seeded), TestContext.CurrentContext.CancellationToken);

    var firstStore = CreateStore(fixture.CreateContext(), clock);
    var secondStore = CreateStore(fixture.CreateContext(), clock);

    ErrorOr<EnrolmentRedemptionResult>[] results = await Task.WhenAll(Task.Run(() => firstStore.RedeemAsync(created.QRCodeValue, null, "First phone", "de", TestContext.CurrentContext.CancellationToken)),
                                                             Task.Run(() => secondStore.RedeemAsync(created.QRCodeValue, null, "Second phone", "de", TestContext.CurrentContext.CancellationToken)));

    var verificationContext = fixture.CreateContext();
    var deviceCount = await verificationContext.Devices.CountAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(results.Count(result => result.IsSuccess), Is.EqualTo(1));
                      Assert.That(deviceCount, Is.EqualTo(1));
                    });
  }

  [Test]
  public async Task RedeemAsync_SuccessfulRedemption_CarriesAPlaintextTokenThatVerifies()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    FakeTimeProvider clock = new(new(2026, 8, 27, 18, 0, 0, TimeSpan.Zero));
    Pbkdf2SecretHasher secretHasher = new();
    DeviceOwnerStore ownerStore = new(fixture.DbContext);
    DeviceTokenStore deviceTokenStore = new(fixture.DbContext, ownerStore, secretHasher, clock);
    EnrolmentInvitationStore store = new(fixture.DbContext, ownerStore, secretHasher, deviceTokenStore, new ImmediateTransactionRunner(fixture.DbContext, new(), new(), NullLogger<ImmediateTransactionRunner>.Instance), clock);

    var created = await store.CreateAsync(await AnnaAsync(fixture.DbContext, seeded), TestContext.CurrentContext.CancellationToken);
    var redemption = await store.RedeemAsync(created.QRCodeValue, null, "Test agent", "de", TestContext.CurrentContext.CancellationToken);

    
    var separatorIndex = redemption.Value.PlaintextToken.IndexOf('.', StringComparison.Ordinal);
    var verifiedOwner = await deviceTokenStore.VerifyAsync(redemption.Value.PlaintextToken[..separatorIndex], redemption.Value.PlaintextToken[(separatorIndex + 1)..], TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(verifiedOwner, Is.Not.Null);
                      Assert.That(verifiedOwner!.Device!.Id, Is.EqualTo(redemption.Value.Owner.Device!.Id));
                    });
  }

  [Test]
  public async Task RedeemAsync_WrongQrCode_CarriesNoPlaintextToken()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var store = CreateStore(fixture, new());

    await store.CreateAsync(await AnnaAsync(fixture.DbContext, seeded), TestContext.CurrentContext.CancellationToken);
    var redemption = await store.RedeemAsync("not-the-right-code", null, "Test agent", "de", TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(redemption.RefusalMessageKey(), Is.EqualTo("enrolment.codeUnknown"));
                    });
  }

  [Test]
  public async Task RedeemAsync_ExpiredInvitation_CarriesNoPlaintextToken()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    FakeTimeProvider clock = new(new(2026, 8, 27, 18, 0, 0, TimeSpan.Zero));
    var store = CreateStore(fixture, clock);

    var created = await store.CreateAsync(await AnnaAsync(fixture.DbContext, seeded), TestContext.CurrentContext.CancellationToken);
    clock.Advance(TimeSpan.FromMinutes(6));

    var redemption = await store.RedeemAsync(created.QRCodeValue, null, "Test agent", "de", TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(redemption.RefusalMessageKey(), Is.EqualTo("enrolment.codeNoLongerValid"));
                    });
  }

  [Test]
  public async Task CreateAsync_AfterAnEarlierInvitationExpiredUnredeemed_ConsumesItAndSucceeds()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    FakeTimeProvider clock = new(new(2026, 8, 27, 18, 0, 0, TimeSpan.Zero));
    var store = CreateStore(fixture, clock);

    var yesterday = await store.CreateAsync(await AnnaAsync(fixture.DbContext, seeded), TestContext.CurrentContext.CancellationToken);

    clock.Advance(TimeSpan.FromDays(1));

    var today = await store.CreateAsync(await AnnaAsync(fixture.DbContext, seeded), TestContext.CurrentContext.CancellationToken);

    var expiredRow = await fixture.DbContext.EnrolmentInvitations.SingleAsync(invitation => invitation.Id == yesterday.Invitation.Id, TestContext.CurrentContext.CancellationToken);

    var currentRow = await fixture.DbContext.EnrolmentInvitations.SingleAsync(invitation => invitation.Id == today.Invitation.Id, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(expiredRow.ConsumedAtUtc, Is.Not.Null, "An expired invitation still holds the single outstanding marker until it is consumed.");
                      Assert.That(currentRow.ConsumedAtUtc, Is.Null);
                    });
  }

  private async Task<IDeviceOwner> AnnaAsync(GastronomyAppDbContext dbContext, SeededDomain seeded)
  {
    return await dbContext.StaffMembers.SingleAsync(staffMember => staffMember.Id == seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);
  }

  private async Task<IDeviceOwner> KitchenAsync(GastronomyAppDbContext dbContext, SeededDomain seeded)
  {
    return await dbContext.Stations.SingleAsync(station => station.Id == seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);
  }

  private EnrolmentInvitationStore CreateStore(SqliteInMemoryFixture fixture, FakeTimeProvider clock)
  {
    return CreateStore(fixture.DbContext, clock);
  }

  private EnrolmentInvitationStore CreateStore(GastronomyAppDbContext dbContext, FakeTimeProvider clock)
  {
    Pbkdf2SecretHasher secretHasher = new();
    DeviceOwnerStore ownerStore = new(dbContext);

    return new(dbContext, ownerStore, secretHasher, new DeviceTokenStore(dbContext, ownerStore, secretHasher, clock), new ImmediateTransactionRunner(dbContext, new(), new(), NullLogger<ImmediateTransactionRunner>.Instance), clock);
  }
}
