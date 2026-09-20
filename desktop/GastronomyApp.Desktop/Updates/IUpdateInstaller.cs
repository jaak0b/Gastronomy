namespace GastronomyApp.Desktop.Updates;

public interface IUpdateInstaller
{
  public bool IsInstalled { get; }

  public bool HasDownloadedUpdate { get; }

  public Task<UpdatePreparation> CheckAndDownloadAsync(CancellationToken cancellationToken);

  public void InstallOnQuit(bool restart);
}
