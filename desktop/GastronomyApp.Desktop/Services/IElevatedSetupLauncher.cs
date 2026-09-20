namespace GastronomyApp.Desktop.Services;

public interface IElevatedSetupLauncher
{
  public Task<ElevatedSetupOutcome> RunElevatedSetupAsync(CancellationToken cancellationToken = default);
}
