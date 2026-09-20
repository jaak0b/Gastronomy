using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Repositories;

[TestFixture]
public sealed class DeviceRepositoryTest
{
  private readonly DateTime _now = new(2026, 8, 27, 18, 0, 0, DateTimeKind.Utc);

  [Test]
  public async Task FindByIdAsync_ADeviceThatWasNeverEnrolled_ReturnsNull()
  {
    using SqliteInMemoryFixture fixture = new();
    DeviceRepository repository = new(fixture.DbContext);

    var device = await repository.FindByIdAsync(Guid.NewGuid(), TestContext.CurrentContext.CancellationToken);

    Assert.That(device, Is.Null);
  }

  [Test]
  public async Task FindByIdAsync_AnEnrolledDevice_ReturnsIt()
  {
    using SqliteInMemoryFixture fixture = new();
    var deviceId = await EnrolDeviceAsync(fixture, "de");

    DeviceRepository repository = new(fixture.DbContext);

    var device = await repository.FindByIdAsync(deviceId, TestContext.CurrentContext.CancellationToken);

    Assert.That(device?.Language, Is.EqualTo("de"));
  }

  [Test]
  public async Task SaveChangesAsync_ALanguageThatChanged_StoresIt()
  {
    using SqliteInMemoryFixture fixture = new();
    var deviceId = await EnrolDeviceAsync(fixture, "de");

    DeviceRepository repository = new(fixture.DbContext);
    var device = (await repository.FindByIdAsync(deviceId, TestContext.CurrentContext.CancellationToken))!;
    device.Language = "en";
    await repository.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    await using var readContext = fixture.CreateContext();
    var stored = await readContext.Devices.FirstAsync(candidate => candidate.Id == deviceId, TestContext.CurrentContext.CancellationToken);

    Assert.That(stored.Language, Is.EqualTo("en"));
  }

  private async Task<Guid> EnrolDeviceAsync(SqliteInMemoryFixture fixture, string language)
  {
    var deviceId = Guid.NewGuid();

    fixture.DbContext.Devices.Add(new()
                                  {
                                    Id = deviceId,
                                    Language = language,
                                    TokenHash = [1],
                                    TokenSalt = [2],
                                    TokenIterations = 1,
                                    TokenAlgorithm = "PBKDF2-HMAC-SHA512",
                                    TokenLookupId = Guid.NewGuid().ToString(),
                                    CreatedAtUtc = _now.AddHours(-1),
                                    LastSeenAtUtc = _now
                                  });

    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return deviceId;
  }
}
