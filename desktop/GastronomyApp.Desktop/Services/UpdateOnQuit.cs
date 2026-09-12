using Serilog;

namespace GastronomyApp.Desktop.Services;

public sealed class UpdateOnQuit
{
  private readonly IUpdateInstallGate _gate;
  private readonly IUpdateInstaller _installer;

  private bool _installDespiteFestival;

  public UpdateOnQuit(IUpdateInstaller installer, IUpdateInstallGate gate)
  {
    _installer = installer;
    _gate = gate;
  }

  public void RequestInstallDespiteFestival()
  {
    _installDespiteFestival = true;
  }

  public async Task PrepareAsync(CancellationToken cancellationToken)
  {
    if (!_installer.HasDownloadedUpdate)
    {
      return;
    }

    try
    {
      if (_installDespiteFestival || await _gate.CanInstallNowAsync(cancellationToken))
      {
        _installer.InstallOnQuit(restart: false);
      }
    }
    catch (Exception failure)
    {
      Log.Error(failure, "Preparing a downloaded update for installation on exit failed.");
    }
  }
}
