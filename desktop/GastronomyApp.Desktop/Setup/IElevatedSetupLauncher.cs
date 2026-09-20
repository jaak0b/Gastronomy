using GastronomyApp.Desktop.Enums;

namespace GastronomyApp.Desktop.Setup;

public interface IElevatedSetupLauncher
{
  public Task<ElevatedSetupOutcome> RunElevatedSetupAsync(CancellationToken cancellationToken = default);
}
