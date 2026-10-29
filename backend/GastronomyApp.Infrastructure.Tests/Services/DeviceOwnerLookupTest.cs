using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure.Security;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Services;

[TestFixture]
public sealed class DeviceOwnerLookupTest
{
  [SetUp]
  public async Task SetUp()
  {
    _fixture = new();
    _seeded = await new DomainSeeder().SeedAsync(_fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    _composition = new(_fixture.DbContext, TimeProvider.System);
  }

  [TearDown]
  public void TearDown()
  {
    _fixture.Dispose();
  }

  private EnrolmentComposition _composition = null!;
  private SqliteInMemoryFixture _fixture = null!;
  private SeededDomain _seeded = null!;

  [Test]
  public async Task FindByTokenAsync_TheTokenOfAWaitersDevice_NamesTheWaiterAndTheDevice()
  {
    var device = await GiveAsync(_fixture.DbContext.StaffMembers.Where(staffMember => staffMember.Id == _seeded.StaffMemberId));

    var owner = await _composition.Lookup.FindByTokenAsync(device.TokenLookupId, Secret, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(owner, Is.InstanceOf<StaffMember>());
                      Assert.That(owner!.Id, Is.EqualTo(_seeded.StaffMemberId));
                      Assert.That(owner.Device!.Id, Is.EqualTo(device.Id));
                    });
  }

  [Test]
  public async Task FindByTokenAsync_TheTokenOfAStationsDevice_NamesTheStation()
  {
    var device = await GiveAsync(_fixture.DbContext.Stations.Where(station => station.Id == _seeded.KitchenStationId));

    var owner = await _composition.Lookup.FindByTokenAsync(device.TokenLookupId, Secret, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(owner, Is.InstanceOf<Station>());
                      Assert.That(owner!.Id, Is.EqualTo(_seeded.KitchenStationId));
                    });
  }

  [Test]
  public async Task FindByTokenAsync_AWrongSecret_NamesNobody()
  {
    var device = await GiveAsync(_fixture.DbContext.StaffMembers.Where(staffMember => staffMember.Id == _seeded.StaffMemberId));

    var owner = await _composition.Lookup.FindByTokenAsync(device.TokenLookupId, "wrong-secret", TestContext.CurrentContext.CancellationToken);

    Assert.That(owner, Is.Null);
  }

  [Test]
  public async Task FindByTokenAsync_AnUnknownLookupId_NamesNobody()
  {
    var owner = await _composition.Lookup.FindByTokenAsync(Guid.NewGuid().ToString("N"), "any-secret", TestContext.CurrentContext.CancellationToken);

    Assert.That(owner, Is.Null);
  }

  [Test]
  public async Task FindByTokenAsync_ARevokedDevice_NamesNobodyAndTheOwnerNoLongerPointsAtIt()
  {
    var device = await GiveAsync(_fixture.DbContext.StaffMembers.Where(staffMember => staffMember.Id == _seeded.StaffMemberId));

    await _composition.Retirement.RevokeDeviceAsync(device.Id, TestContext.CurrentContext.CancellationToken);
    var owner = await _composition.Lookup.FindByTokenAsync(device.TokenLookupId, Secret, TestContext.CurrentContext.CancellationToken);
    var anna = await _fixture.DbContext.StaffMembers.SingleAsync(staffMember => staffMember.Id == _seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(owner, Is.Null);
                      Assert.That(anna.DeviceId, Is.Null);
                    });
  }

  [Test]
  public async Task FindByDeviceAsync_ADeviceNobodyOwns_NamesNobody()
  {
    var owner = await _composition.Lookup.FindByDeviceAsync(Guid.NewGuid(), TestContext.CurrentContext.CancellationToken);

    Assert.That(owner, Is.Null);
  }

  private const string Secret = "known-secret";

  private async Task<Device> GiveAsync<TOwner>(IQueryable<TOwner> owners) where TOwner : class, IDeviceOwner
  {
    var hashed = new SecretHasher().Hash(Secret);
    Device device = new()
                    {
                      Id = Guid.NewGuid(),
                      Language = "de",
                      TokenHash = hashed.Hash,
                      TokenSalt = hashed.Salt,
                      TokenAlgorithm = hashed.Algorithm,
                      TokenLookupId = Guid.NewGuid().ToString("N"),
                      CreatedAtUtc = DateTime.UtcNow
                    };

    _fixture.DbContext.Devices.Add(device);
    var owner = await owners.SingleAsync(TestContext.CurrentContext.CancellationToken);
    owner.DeviceId = device.Id;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    return device;
  }
}
