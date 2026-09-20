using System.ComponentModel;
using System.Diagnostics;

namespace GastronomyApp.Desktop.Services.Windows;

public sealed class WindowsElevatedSetupLauncher : IElevatedSetupLauncher
{
  private const int ElevationDeclinedByUser = 1223;
  private const int SetupFinishedExitCode = 0;
  private const string SetupArgument = "--setup";

  private readonly string _executablePath;

  public WindowsElevatedSetupLauncher(string executablePath)
  {
    _executablePath = executablePath;
  }

  public async Task<ElevatedSetupOutcome> RunElevatedSetupAsync(CancellationToken cancellationToken = default)
  {
    ProcessStartInfo startInfo = new()
                                 {
                                   FileName = _executablePath,
                                   Arguments = SetupArgument,
                                   UseShellExecute = true,
                                   Verb = "runas"
                                 };

    try
    {
      using var process = Process.Start(startInfo);
      if (process is null)
        return ElevatedSetupOutcome.ElevationDeclined;

      await process.WaitForExitAsync(cancellationToken);

      if (process.ExitCode == SetupFinishedExitCode)
        return ElevatedSetupOutcome.Completed;

      return ElevatedSetupOutcome.SetupStepFailed;
    }
    catch (Win32Exception failure) when (failure.NativeErrorCode == ElevationDeclinedByUser)
    {
      return ElevatedSetupOutcome.ElevationDeclined;
    }
  }
}
