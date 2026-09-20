using GastronomyApp.Desktop.Enums;

namespace GastronomyApp.Desktop.Ports;

public interface IElevatedSetupLauncher
{
  public Task<ElevatedSetupOutcome> RunElevatedSetupAsync(CancellationToken cancellationToken = default);
}
