using Serilog;
using GastronomyApp.Desktop.Ports;

namespace GastronomyApp.Desktop.Updates;

public sealed class UpdateOnQuit
{
  private readonly IUpdateInstallGate _gate;
  private readonly IUpdateInstaller _installer;

  public UpdateOnQuit(IUpdateInstaller installer, IUpdateInstallGate gate)
  {
    _installer = installer;
    _gate = gate;
  }

  public async Task PrepareAsync(CancellationToken cancellationToken)
  {
    if (!_installer.HasDownloadedUpdate)
      return;

    try
    {
      if (await _gate.CanInstallNowAsync(cancellationToken))
        _installer.InstallOnQuit(false);
    }
    catch (Exception failure)
    {
      Log.Error(failure, "Preparing a downloaded update for installation on exit failed.");
    }
  }
}
