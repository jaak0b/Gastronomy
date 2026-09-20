using FakeItEasy;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class DeviceLanguageServiceTest
{
  [SetUp]
  public void SetUp()
  {
    _repository = A.Fake<IDeviceRepository>();
    A.CallTo(() => _repository.FindByIdAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<Device?>(null));

    _service = new(_repository);
  }

  private readonly Guid _deviceId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

  private IDeviceRepository _repository = null!;
  private DeviceLanguageService _service = null!;

  [Test]
  public async Task ChangeAsync_ALanguageTheAppDoesNotSpeak_RefusesItAndSavesNothing()
  {
    Result<ChangedDeviceLanguage, DeviceLanguageFailure> changed = await _service.ChangeAsync(_deviceId, "fr", CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(changed.IsSuccess, Is.False);
                      Assert.That(changed.Failure.Reason, Is.EqualTo(DeviceLanguageFailureReason.UnsupportedLanguage));
                    });

    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task ChangeAsync_NoLanguageAtAll_RefusesItAsUnsupported()
  {
    Result<ChangedDeviceLanguage, DeviceLanguageFailure> changed = await _service.ChangeAsync(_deviceId, null, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(changed.IsSuccess, Is.False);
                      Assert.That(changed.Failure.Reason, Is.EqualTo(DeviceLanguageFailureReason.UnsupportedLanguage));
                    });
  }

  [Test]
  public async Task ChangeAsync_ADeviceThatWasSetUpAgainElsewhere_RefusesBecauseTheDeviceIsGone()
  {
    Result<ChangedDeviceLanguage, DeviceLanguageFailure> changed = await _service.ChangeAsync(_deviceId, "en", CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(changed.IsSuccess, Is.False);
                      Assert.That(changed.Failure.Reason, Is.EqualTo(DeviceLanguageFailureReason.DeviceNotFound));
                    });
  }

  [Test]
  public async Task ChangeAsync_ALanguageTheAppSpeaks_StoresItOnTheDevice()
  {
    var device = GivenTheDeviceSpeaks("de");

    Result<ChangedDeviceLanguage, DeviceLanguageFailure> changed = await _service.ChangeAsync(_deviceId, "en", CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(changed.IsSuccess, Is.True);
                      Assert.That(changed.Value.DeviceId, Is.EqualTo(_deviceId));
                      Assert.That(changed.Value.Language, Is.EqualTo("en"));
                      Assert.That(device.Language, Is.EqualTo("en"));
                    });

    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  private Device GivenTheDeviceSpeaks(string language)
  {
    Device device = new()
                    {
                      Id = _deviceId,
                      Language = language,
                      TokenHash = [1],
                      TokenSalt = [2],
                      TokenIterations = 1,
                      TokenAlgorithm = "PBKDF2-HMAC-SHA512",
                      TokenLookupId = Guid.NewGuid().ToString(),
                      CreatedAtUtc = new(2026, 9, 5, 18, 0, 0, DateTimeKind.Utc),
                      LastSeenAtUtc = new(2026, 9, 5, 19, 0, 0, DateTimeKind.Utc)
                    };

    A.CallTo(() => _repository.FindByIdAsync(_deviceId, A<CancellationToken>._)).Returns(Task.FromResult<Device?>(device));

    return device;
  }
}
