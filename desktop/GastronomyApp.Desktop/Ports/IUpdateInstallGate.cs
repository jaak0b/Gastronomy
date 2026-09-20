namespace GastronomyApp.Desktop.Ports;

public interface IUpdateInstallGate
{
  public Task<bool> CanInstallNowAsync(CancellationToken cancellationToken);
}
