using Serilog;

namespace GastronomyApp.Desktop.Services;

public sealed class AutomaticUpdateChecker
{
  private readonly IUpdateInstaller _installer;
  private readonly ISettingsStore _settingsStore;

  public AutomaticUpdateChecker(IUpdateInstaller installer, ISettingsStore settingsStore)
  {
    _installer = installer;
    _settingsStore = settingsStore;
  }

  public async Task CheckOnStartupAsync(CancellationToken cancellationToken = default)
  {
    try
    {
      if (!_installer.IsInstalled)
      {
        return;
      }

      var settings = _settingsStore.Load();
      var attemptAt = DateTimeOffset.UtcNow;

      if (settings.LastUpdateCheckUtc is { } lastCheck
          && attemptAt - lastCheck < TimeSpan.FromHours(1))
      {
        return;
      }

      _settingsStore.Save(settings with { LastUpdateCheckUtc = attemptAt });

      await _installer.CheckAndDownloadAsync(cancellationToken);
    }
    catch (Exception failure)
    {
      Log.Error(failure, "The automatic update check failed unexpectedly.");
    }
  }
}
