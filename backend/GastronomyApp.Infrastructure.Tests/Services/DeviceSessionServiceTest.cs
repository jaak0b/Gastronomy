using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Services;

[TestFixture]
public sealed class DeviceSessionServiceTest
{
  [SetUp]
  public async Task SetUp()
  {
    _fixture = new();
    _seeded = await new DomainSeeder().SeedAsync(_fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    _service = new(_fixture.DbContext, new CoreMappingFactory().Create());
  }

  [TearDown]
  public void TearDown()
  {
    _fixture.Dispose();
  }

  private SqliteInMemoryFixture _fixture = null!;
  private SeededDomain _seeded = null!;
  private DeviceSessionService _service = null!;

  [Test]
  public async Task ReadAsync_AWaitersDevice_NamesTheWaiterWithTheDeviceAndItsLanguage()
  {
    var device = await GiveAsync("en", _fixture.DbContext.StaffMembers.Where(staffMember => staffMember.Id == _seeded.StaffMemberId));

    var session = await _service.ReadAsync(device.Id, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(session.IsError, Is.False);
                      Assert.That(session.Value.DeviceId, Is.EqualTo(device.Id));
                      Assert.That(session.Value.Language, Is.EqualTo("en"));
                      Assert.That(session.Value.StaffMember!.Id, Is.EqualTo(_seeded.StaffMemberId));
                      Assert.That(session.Value.StaffMember.Name, Is.EqualTo("Anna"));
                      Assert.That(session.Value.Station, Is.Null);
                    });
  }

  [Test]
  public async Task ReadAsync_AStationsDevice_NamesTheStation()
  {
    var device = await GiveAsync("de", _fixture.DbContext.Stations.Where(station => station.Id == _seeded.KitchenStationId));

    var session = await _service.ReadAsync(device.Id, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(session.IsError, Is.False);
                      Assert.That(session.Value.DeviceId, Is.EqualTo(device.Id));
                      Assert.That(session.Value.Station!.Id, Is.EqualTo(_seeded.KitchenStationId));
                      Assert.That(session.Value.Station.Name, Is.EqualTo("Kueche"));
                      Assert.That(session.Value.StaffMember, Is.Null);
                    });
  }

  [Test]
  public async Task ReadAsync_ADeviceNobodyOwnsAnyMore_IsRefusedAsAnUnknownOwner()
  {
    var session = await _service.ReadAsync(Guid.NewGuid(), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(session.IsError, Is.True);
                      Assert.That(session.RefusalMessageKey(), Is.EqualTo("OwnerUnknown"));
                      Assert.That(session.FirstError.NumericType, Is.EqualTo(401));
                    });
  }

  [Test]
  public async Task ChangeLanguageAsync_ALanguageTheAppDoesNotSpeak_RefusesItAndLeavesTheDeviceAsItWas()
  {
    var device = await GiveAsync("de", _fixture.DbContext.StaffMembers.Where(staffMember => staffMember.Id == _seeded.StaffMemberId));

    var changed = await _service.ChangeLanguageAsync(device.Id, new() { Language = "fr" }, TestContext.CurrentContext.CancellationToken);
    var stored = await _fixture.DbContext.Devices.SingleAsync(candidate => candidate.Id == device.Id, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(changed.IsError, Is.True);
                      Assert.That(changed.RefusalMessageKey(), Is.EqualTo("errors.session.unsupportedLanguage"));
                      Assert.That(stored.Language, Is.EqualTo("de"));
                    });
  }

  [Test]
  public async Task ChangeLanguageAsync_NoLanguageAtAll_RefusesItAsUnsupported()
  {
    var changed = await _service.ChangeLanguageAsync(Guid.NewGuid(), new() { Language = null }, TestContext.CurrentContext.CancellationToken);

    Assert.That(changed.RefusalMessageKey(), Is.EqualTo("errors.session.unsupportedLanguage"));
  }

  [Test]
  public async Task ChangeLanguageAsync_ADeviceThatWasSetUpAgainElsewhere_RefusesBecauseTheDeviceIsGone()
  {
    var changed = await _service.ChangeLanguageAsync(Guid.NewGuid(), new() { Language = "en" }, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(changed.IsError, Is.True);
                      Assert.That(changed.RefusalMessageKey(), Is.EqualTo("DeviceNotFound"));
                    });
  }

  [Test]
  public async Task ChangeLanguageAsync_ALanguageTheAppSpeaks_StoresItOnTheDevice()
  {
    var device = await GiveAsync("de", _fixture.DbContext.StaffMembers.Where(staffMember => staffMember.Id == _seeded.StaffMemberId));

    var changed = await _service.ChangeLanguageAsync(device.Id, new() { Language = "en" }, TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var stored = await _fixture.DbContext.Devices.SingleAsync(candidate => candidate.Id == device.Id, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(changed.IsError, Is.False);
                      Assert.That(stored.Language, Is.EqualTo("en"));
                    });
  }

  private async Task<Device> GiveAsync<TOwner>(string language, IQueryable<TOwner> owners) where TOwner : class, IDeviceOwner
  {
    Device device = new()
                    {
                      Id = Guid.NewGuid(),
                      Language = language,
                      TokenHash = [1],
                      TokenSalt = [2],
                      TokenIterations = 1,
                      TokenAlgorithm = "PBKDF2-HMAC-SHA512",
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
