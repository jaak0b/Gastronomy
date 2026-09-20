namespace GastronomyApp.Desktop.Services;

public interface IUpdateInstallGate
{
  public Task<bool> CanInstallNowAsync(CancellationToken cancellationToken);
}
