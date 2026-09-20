using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class DeviceLanguageService
{
  private readonly IDeviceRepository _repository;

  private readonly IReadOnlyCollection<string> _supportedLanguages =
  [
    "de",
    "en"
  ];

  public DeviceLanguageService(IDeviceRepository repository)
  {
    _repository = repository;
  }

  public async Task<Result<ChangedDeviceLanguage, DeviceLanguageFailure>> ChangeAsync(Guid deviceId, string? language, CancellationToken cancellationToken)
  {
    if (language is null || !_supportedLanguages.Contains(language, StringComparer.Ordinal))
      return Failed(DeviceLanguageFailureReason.UnsupportedLanguage);

    var device = await _repository.FindByIdAsync(deviceId, cancellationToken);

    if (device is null)
      return Failed(DeviceLanguageFailureReason.DeviceNotFound);

    device.Language = language;
    await _repository.SaveChangesAsync(cancellationToken);

    return Result<ChangedDeviceLanguage, DeviceLanguageFailure>.Success(new()
                                                                        {
                                                                          DeviceId = deviceId,
                                                                          Language = language
                                                                        });
  }

  private Result<ChangedDeviceLanguage, DeviceLanguageFailure> Failed(DeviceLanguageFailureReason reason)
  {
    return Result<ChangedDeviceLanguage, DeviceLanguageFailure>.Failed(new() { Reason = reason });
  }
}
