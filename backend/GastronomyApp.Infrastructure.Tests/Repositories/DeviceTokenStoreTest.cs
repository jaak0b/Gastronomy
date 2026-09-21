using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Repositories;

public sealed class DeviceTokenStoreTest
{
  [Test]
  public async Task IssueAsync_ThenVerifyAsync_WithReturnedSecret_IsValidAndNamesTheStaffMember()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var store = CreateStore(fixture);

    var issued = await store.IssueAsync(await StaffMemberOwnerAsync(fixture, seeded), "de", "Test agent", TestContext.CurrentContext.CancellationToken);
    var parts = SplitToken(issued.PlaintextToken);

    var owner = await store.VerifyAsync(parts.TokenLookupId, parts.Secret, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(owner, Is.Not.Null);
                      Assert.That(owner!.Device!.Id, Is.EqualTo(issued.Device.Id));
                      Assert.That(owner.Id, Is.EqualTo(seeded.StaffMemberId));
                    });
  }

  [Test]
  public async Task IssueAsync_ForAStation_PointsTheStationAtTheNewDevice()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var store = CreateStore(fixture);

    var issued = await store.IssueAsync(await KitchenOwnerAsync(fixture, seeded), "de", "Tablet", TestContext.CurrentContext.CancellationToken);
    var parts = SplitToken(issued.PlaintextToken);

    var owner = await store.VerifyAsync(parts.TokenLookupId, parts.Secret, TestContext.CurrentContext.CancellationToken);
    var kitchen = await fixture.DbContext.Stations.SingleAsync(station => station.Id == seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(owner!.Id, Is.EqualTo(seeded.KitchenStationId));
                      Assert.That(kitchen.DeviceId, Is.EqualTo(issued.Device.Id));
                    });
  }

  [Test]
  public async Task IssueAsync_ASecondTimeForTheSameOwner_DeletesTheEarlierDeviceSoItsTokenStopsWorking()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var store = CreateStore(fixture);

    var first = await store.IssueAsync(await StaffMemberOwnerAsync(fixture, seeded), "de", "Old phone", TestContext.CurrentContext.CancellationToken);
    var second = await store.IssueAsync(await StaffMemberOwnerAsync(fixture, seeded), "de", "New phone", TestContext.CurrentContext.CancellationToken);
    var firstParts = SplitToken(first.PlaintextToken);

    var firstOwner = await store.VerifyAsync(firstParts.TokenLookupId, firstParts.Secret, TestContext.CurrentContext.CancellationToken);
    var staffMember = await fixture.DbContext.StaffMembers.SingleAsync(candidate => candidate.Id == seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);
    var deviceCount = await fixture.DbContext.Devices.CountAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(firstOwner, Is.Null);
                      Assert.That(staffMember.DeviceId, Is.EqualTo(second.Device.Id));
                      Assert.That(deviceCount, Is.EqualTo(1));
                    });
  }

  [Test]
  public async Task VerifyAsync_WrongSecret_IsInvalid()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var store = CreateStore(fixture);

    var issued = await store.IssueAsync(await StaffMemberOwnerAsync(fixture, seeded), "de", "Test agent", TestContext.CurrentContext.CancellationToken);
    var parts = SplitToken(issued.PlaintextToken);

    var owner = await store.VerifyAsync(parts.TokenLookupId, "wrong-secret", TestContext.CurrentContext.CancellationToken);

    Assert.That(owner, Is.Null);
  }

  [Test]
  public async Task VerifyAsync_RevokedDevice_IsInvalidAndTheOwnerNoLongerPointsAtIt()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var store = CreateStore(fixture);

    var issued = await store.IssueAsync(await StaffMemberOwnerAsync(fixture, seeded), "de", "Test agent", TestContext.CurrentContext.CancellationToken);
    var parts = SplitToken(issued.PlaintextToken);

    await store.RevokeAsync(issued.Device.Id, TestContext.CurrentContext.CancellationToken);
    var owner = await store.VerifyAsync(parts.TokenLookupId, parts.Secret, TestContext.CurrentContext.CancellationToken);
    var staffMember = await fixture.DbContext.StaffMembers.SingleAsync(candidate => candidate.Id == seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(owner, Is.Null);
                      Assert.That(staffMember.DeviceId, Is.Null);
                    });
  }

  [Test]
  public async Task VerifyAsync_UnknownTokenLookupId_IsInvalid()
  {
    using SqliteInMemoryFixture fixture = new();
    var store = CreateStore(fixture);

    var owner = await store.VerifyAsync(Guid.NewGuid().ToString("N"), "any-secret", TestContext.CurrentContext.CancellationToken);

    Assert.That(owner, Is.Null);
  }

  [Test]
  public async Task IssueAsync_NewToken_NeverPersistsPlaintextSecretAnywhere()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var store = CreateStore(fixture);

    var issued = await store.IssueAsync(await StaffMemberOwnerAsync(fixture, seeded), "de", "Test agent", TestContext.CurrentContext.CancellationToken);
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

          if (value is byte[] bytes)
            storedValues.Add(Convert.ToHexString(bytes));
          else
            storedValues.Add(value.ToString() ?? string.Empty);
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

  private async Task<IDeviceOwner> StaffMemberOwnerAsync(SqliteInMemoryFixture fixture, SeededDomain seeded)
  {
    return await fixture.DbContext.StaffMembers.SingleAsync(candidate => candidate.Id == seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);
  }

  private async Task<IDeviceOwner> KitchenOwnerAsync(SqliteInMemoryFixture fixture, SeededDomain seeded)
  {
    return await fixture.DbContext.Stations.SingleAsync(candidate => candidate.Id == seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);
  }

  private TokenParts SplitToken(string plaintextToken)
  {
    var separatorIndex = plaintextToken.IndexOf('.', StringComparison.Ordinal);

    return new(plaintextToken[..separatorIndex], plaintextToken[(separatorIndex + 1)..]);
  }

  private DeviceTokenStore CreateStore(SqliteInMemoryFixture fixture)
  {
    return new(fixture.DbContext, new DeviceOwnerStore(fixture.DbContext), new(), new SystemClock());
  }

  private sealed record TokenParts(string TokenLookupId, string Secret);
}
