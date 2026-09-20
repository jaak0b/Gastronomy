namespace GastronomyApp.Desktop.Updates;

public interface IUpdateInstallGate
{
  public Task<bool> CanInstallNowAsync(CancellationToken cancellationToken);
}
