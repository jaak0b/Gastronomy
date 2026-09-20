namespace GastronomyApp.Desktop.Services.Windows;

public sealed class UnavailableElevatedSetupLauncher : IElevatedSetupLauncher
{
  public Task<ElevatedSetupOutcome> RunElevatedSetupAsync(CancellationToken cancellationToken = default)
  {
    return Task.FromResult(ElevatedSetupOutcome.ElevationDeclined);
  }
}
