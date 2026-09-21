using ErrorOr;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;

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

  public async Task<ErrorOr<Device>> ChangeAsync(Guid deviceId, string? language, CancellationToken cancellationToken)
  {
    if (language is null || !_supportedLanguages.Contains(language, StringComparer.Ordinal))
      return Refusal.DeviceLanguage.UnsupportedLanguage(language);

    var device = await _repository.FindByIdAsync(deviceId, cancellationToken);

    if (device is null)
      return Refusal.DeviceLanguage.DeviceNotFound(deviceId);

    device.Language = language;
    await _repository.SaveChangesAsync(cancellationToken);

    return device;
  }
}
