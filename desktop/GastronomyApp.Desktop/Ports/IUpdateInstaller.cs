using GastronomyApp.Desktop.Values;

namespace GastronomyApp.Desktop.Ports;

public interface IUpdateInstaller
{
  public bool IsInstalled { get; }

  public bool HasDownloadedUpdate { get; }

  public Task<UpdatePreparation> CheckAndDownloadAsync(CancellationToken cancellationToken);

  public void InstallOnQuit(bool restart);
}
