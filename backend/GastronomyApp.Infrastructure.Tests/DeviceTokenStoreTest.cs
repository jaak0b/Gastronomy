using GastronomyApp.Core.Enums;
using GastronomyApp.Infrastructure.Ports;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests;

public sealed class DeviceTokenStoreTest
{
  [Test]
  public async Task IssueAsync_ThenVerifyAsync_WithReturnedSecret_IsValidAndNamesTheStaffMember()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var store = CreateStore(fixture);

    var issued = await store.IssueAsync(StaffMemberOwner(seeded), "de", "Test agent", TestContext.CurrentContext.CancellationToken);
    var parts = SplitToken(issued.PlaintextToken);

    var verification = await store.VerifyAsync(parts.TokenLookupId, parts.Secret, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(verification.IsValid, Is.True);
                      Assert.That(verification.Device!.Id, Is.EqualTo(issued.Device.Id));
                      Assert.That(verification.Owner, Is.EqualTo(StaffMemberOwner(seeded)));
                    });
  }

  [Test]
  public async Task IssueAsync_ForAStation_PointsTheStationAtTheNewDevice()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var store = CreateStore(fixture);

    var issued = await store.IssueAsync(KitchenOwner(seeded), "de", "Tablet", TestContext.CurrentContext.CancellationToken);
    var parts = SplitToken(issued.PlaintextToken);

    var verification = await store.VerifyAsync(parts.TokenLookupId, parts.Secret, TestContext.CurrentContext.CancellationToken);
    var kitchen = await fixture.DbContext.Stations.SingleAsync(station => station.Id == seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(verification.Owner, Is.EqualTo(KitchenOwner(seeded)));
                      Assert.That(kitchen.DeviceId, Is.EqualTo(issued.Device.Id));
                    });
  }

  [Test]
  public async Task IssueAsync_ASecondTimeForTheSameOwner_DeletesTheEarlierDeviceSoItsTokenStopsWorking()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var store = CreateStore(fixture);

    var first = await store.IssueAsync(StaffMemberOwner(seeded), "de", "Old phone", TestContext.CurrentContext.CancellationToken);
    var second = await store.IssueAsync(StaffMemberOwner(seeded), "de", "New phone", TestContext.CurrentContext.CancellationToken);
    var firstParts = SplitToken(first.PlaintextToken);

    var firstVerification = await store.VerifyAsync(firstParts.TokenLookupId, firstParts.Secret, TestContext.CurrentContext.CancellationToken);
    var staffMember = await fixture.DbContext.StaffMembers.SingleAsync(candidate => candidate.Id == seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);
    var deviceCount = await fixture.DbContext.Devices.CountAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(firstVerification.IsValid, Is.False);
                      Assert.That(staffMember.DeviceId, Is.EqualTo(second.Device.Id));
                      Assert.That(deviceCount, Is.EqualTo(1));
                    });
  }

  [Test]
  public void IssueAsync_ForAnOwnerThatDoesNotExist_Throws()
  {
    using SqliteInMemoryFixture fixture = new();
    var store = CreateStore(fixture);

    Assert.That(async () => await store.IssueAsync(new(DeviceOwnerKind.StaffMember, Guid.NewGuid()), "de", "Test agent", TestContext.CurrentContext.CancellationToken),
                Throws.InstanceOf<InvalidOperationException>());
  }

  [Test]
  public async Task VerifyAsync_WrongSecret_IsInvalid()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var store = CreateStore(fixture);

    var issued = await store.IssueAsync(StaffMemberOwner(seeded), "de", "Test agent", TestContext.CurrentContext.CancellationToken);
    var parts = SplitToken(issued.PlaintextToken);

    var verification = await store.VerifyAsync(parts.TokenLookupId, "wrong-secret", TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(verification.IsValid, Is.False);
                      Assert.That(verification.Device, Is.Null);
                      Assert.That(verification.Owner, Is.Null);
                    });
  }

  [Test]
  public async Task VerifyAsync_RevokedDevice_IsInvalidAndTheOwnerNoLongerPointsAtIt()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var store = CreateStore(fixture);

    var issued = await store.IssueAsync(StaffMemberOwner(seeded), "de", "Test agent", TestContext.CurrentContext.CancellationToken);
    var parts = SplitToken(issued.PlaintextToken);

    await store.RevokeAsync(issued.Device.Id, TestContext.CurrentContext.CancellationToken);
    var verification = await store.VerifyAsync(parts.TokenLookupId, parts.Secret, TestContext.CurrentContext.CancellationToken);
    var staffMember = await fixture.DbContext.StaffMembers.SingleAsync(candidate => candidate.Id == seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(verification.IsValid, Is.False);
                      Assert.That(staffMember.DeviceId, Is.Null);
                    });
  }

  [Test]
  public async Task VerifyAsync_UnknownTokenLookupId_IsInvalid()
  {
    using SqliteInMemoryFixture fixture = new();
    var store = CreateStore(fixture);

    var verification = await store.VerifyAsync(Guid.NewGuid().ToString("N"), "any-secret", TestContext.CurrentContext.CancellationToken);

    Assert.That(verification.IsValid, Is.False);
  }

  [Test]
  public async Task IssueAsync_NewToken_NeverPersistsPlaintextSecretAnywhere()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var store = CreateStore(fixture);

    var issued = await store.IssueAsync(StaffMemberOwner(seeded), "de", "Test agent", TestContext.CurrentContext.CancellationToken);
    var parts = SplitToken(issued.PlaintextToken);

    List<string> storedValues = [];
    using (var command = fixture.Connection.CreateCommand())
    {
      command.CommandText = "SELECT * FROM Devices";
      using var reader = await command.ExecuteReaderAsync(TestContext.CurrentContext.CancellationToken);
      while (await reader.ReadAsync(TestContext.CurrentContext.CancellationToken))
      {
        for (var column = 0; column < reader.FieldCount; column++)
        {
          var value = reader.GetValue(column);
          storedValues.Add(value is byte[] bytes ? Convert.ToHexString(bytes) : value.ToString() ?? string.Empty);
        }
      }
    }

    Assert.Multiple(() =>
                    {
                      Assert.That(storedValues, Is.Not.Empty);
                      Assert.That(storedValues.Any(value => value.Contains(parts.Secret, StringComparison.Ordinal)), Is.False);
                      Assert.That(storedValues.Any(value => value.Contains(issued.PlaintextToken, StringComparison.Ordinal)), Is.False);
                    });
  }

  private DeviceOwner StaffMemberOwner(SeededDomain seeded)
  {
    return new(DeviceOwnerKind.StaffMember, seeded.StaffMemberId);
  }

  private DeviceOwner KitchenOwner(SeededDomain seeded)
  {
    return new(DeviceOwnerKind.Station, seeded.KitchenStationId);
  }

  private TokenParts SplitToken(string plaintextToken)
  {
    var separatorIndex = plaintextToken.IndexOf('.', StringComparison.Ordinal);

    return new(plaintextToken[..separatorIndex],
               plaintextToken[(separatorIndex + 1)..]);
  }

  private DeviceTokenStore CreateStore(SqliteInMemoryFixture fixture)
  {
    return new(fixture.DbContext, new(fixture.DbContext), new(), new SystemClock());
  }

  private sealed record TokenParts(string TokenLookupId, string Secret);
}
