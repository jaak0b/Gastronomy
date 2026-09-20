using GastronomyApp.Desktop.Enums;
using GastronomyApp.Desktop.Setup;

namespace GastronomyApp.Desktop.Platform.Windows;

public sealed class UnavailableElevatedSetupLauncher : IElevatedSetupLauncher
{
  public Task<ElevatedSetupOutcome> RunElevatedSetupAsync(CancellationToken cancellationToken = default)
  {
    return Task.FromResult(ElevatedSetupOutcome.ElevationDeclined);
  }
}
