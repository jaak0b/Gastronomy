using System.ComponentModel;
using System.Diagnostics;

namespace GastronomyApp.Desktop.Services.Windows;

public sealed class WindowsElevatedSetupLauncher : IElevatedSetupLauncher
{
  private const int ElevationDeclinedByUser = 1223;
  private const string SetupArgument = "--setup";

  private readonly string executablePath;

  public WindowsElevatedSetupLauncher(string executablePath)
  {
    this.executablePath = executablePath;
  }

  public async Task<ElevatedSetupOutcome> RunElevatedSetupAsync(CancellationToken cancellationToken = default)
  {
    ProcessStartInfo startInfo = new()
                                 {
                                   FileName = executablePath,
                                   Arguments = SetupArgument,
                                   UseShellExecute = true,
                                   Verb = "runas"
                                 };

    try
    {
      using var process = Process.Start(startInfo);
      if (process is null)
      {
        return ElevatedSetupOutcome.ElevationDeclined;
      }

      await process.WaitForExitAsync(cancellationToken);

      return process.ExitCode == 0
               ? ElevatedSetupOutcome.Completed
               : ElevatedSetupOutcome.ElevationDeclined;
    }
    catch (Win32Exception failure) when (failure.NativeErrorCode == ElevationDeclinedByUser)
    {
      return ElevatedSetupOutcome.ElevationDeclined;
    }
  }
}

public sealed class UnavailableElevatedSetupLauncher : IElevatedSetupLauncher
{
  public Task<ElevatedSetupOutcome> RunElevatedSetupAsync(CancellationToken cancellationToken = default)
  {
    return Task.FromResult(ElevatedSetupOutcome.ElevationDeclined);
  }
}
