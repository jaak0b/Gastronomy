using GastronomyApp.Core.ReadModels;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests;

[TestFixture]
public sealed class StaffMemberRepositoryTest
{
  private readonly DateTime _now = new(2026, 8, 27, 18, 0, 0, DateTimeKind.Utc);

  [Test]
  public async Task FindAdministeredAsync_SomebodyWithoutAPhone_CarriesNoDeviceAndNoLastSeenMoment()
  {
    using SqliteInMemoryFixture fixture = new();
    await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    StaffMemberRepository repository = new(fixture.DbContext);

    IReadOnlyList<AdministeredStaffMember> staffMembers =
      await repository.FindAdministeredAsync(_now, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(staffMembers[0].Name, Is.EqualTo("Anna"));
                      Assert.That(staffMembers[0].HasDevice, Is.False);
                      Assert.That(staffMembers[0].LastSeenAtUtc, Is.Null);
                      Assert.That(staffMembers[0].HasOutstandingInvitation, Is.False);
                    });
  }

  [Test]
  public async Task FindAdministeredAsync_SomebodyHoldingAPhone_CarriesWhenThatPhoneWasLastSeen()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var lastSeenAtUtc = _now.AddMinutes(-2);
    await GiveAnnaAPhoneAsync(fixture, seeded, lastSeenAtUtc);

    StaffMemberRepository repository = new(fixture.DbContext);

    IReadOnlyList<AdministeredStaffMember> staffMembers =
      await repository.FindAdministeredAsync(_now, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(staffMembers[0].HasDevice, Is.True);
                      Assert.That(staffMembers[0].LastSeenAtUtc, Is.EqualTo(lastSeenAtUtc));
                    });
  }

  [Test]
  public async Task FindAdministeredAsync_AnInvitationThatWasAlreadyUsed_NoLongerCountsAsOutstanding()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    await InviteAnnaAsync(fixture, seeded, _now.AddMinutes(-1));

    StaffMemberRepository repository = new(fixture.DbContext);

    IReadOnlyList<AdministeredStaffMember> staffMembers =
      await repository.FindAdministeredAsync(_now, TestContext.CurrentContext.CancellationToken);

    Assert.That(staffMembers[0].HasOutstandingInvitation, Is.False);
  }

  [Test]
  public async Task FindByIdAsync_SomebodyWhoIsNotOnTheList_ReturnsNothing()
  {
    using SqliteInMemoryFixture fixture = new();
    await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    StaffMemberRepository repository = new(fixture.DbContext);

    Assert.That(await repository.FindByIdAsync(Guid.NewGuid(), TestContext.CurrentContext.CancellationToken),
                Is.Null);
  }

  [Test]
  public async Task SaveChangesAsync_ANameThatWasJustChanged_StoresIt()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    StaffMemberRepository repository = new(fixture.DbContext);

    var anna = (await repository.FindByIdAsync(seeded.StaffMemberId,
                                               TestContext.CurrentContext.CancellationToken))!;
    anna.Name = "Anne Marie";
    await repository.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    await using var readContext = fixture.CreateContext();

    Assert.That((await readContext.StaffMembers.FirstAsync(staffMember => staffMember.Id == seeded.StaffMemberId,
                                                           TestContext.CurrentContext.CancellationToken)).Name,
                Is.EqualTo("Anne Marie"));
  }

  private async Task GiveAnnaAPhoneAsync(SqliteInMemoryFixture fixture,
                                         SeededDomain seeded,
                                         DateTime lastSeenAtUtc)
  {
    fixture.DbContext.Devices.Add(new()
                                  {
                                    Id = seeded.DeviceId,
                                    Language = "de",
                                    TokenHash = [1],
                                    TokenSalt = [2],
                                    TokenIterations = 1,
                                    TokenAlgorithm = "PBKDF2-HMAC-SHA512",
                                    TokenLookupId = Guid.NewGuid().ToString(),
                                    CreatedAtUtc = _now.AddHours(-1),
                                    LastSeenAtUtc = lastSeenAtUtc
                                  });

    var anna = await fixture.DbContext.StaffMembers
                            .FirstAsync(staffMember => staffMember.Id == seeded.StaffMemberId,
                                        TestContext.CurrentContext.CancellationToken);
    anna.DeviceId = seeded.DeviceId;

    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }

  private async Task InviteAnnaAsync(SqliteInMemoryFixture fixture, SeededDomain seeded, DateTime consumedAtUtc)
  {
    var invitationId = Guid.NewGuid();

    fixture.DbContext.EnrolmentInvitations.Add(new()
                                               {
                                                 Id = invitationId,
                                                 QRCodeHash = [1],
                                                 QRCodeSalt = [2],
                                                 QRCodeIterations = 1,
                                                 QRCodeAlgorithm = "PBKDF2-HMAC-SHA512",
                                                 CreatedAtUtc = _now.AddMinutes(-5),
                                                 ExpiresAtUtc = _now.AddMinutes(5),
                                                 ConsumedAtUtc = consumedAtUtc,
                                                 ConsumedByDeviceId = null
                                               });

    var anna = await fixture.DbContext.StaffMembers
                            .FirstAsync(staffMember => staffMember.Id == seeded.StaffMemberId,
                                        TestContext.CurrentContext.CancellationToken);
    anna.EnrolmentInvitationId = invitationId;

    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }
}
