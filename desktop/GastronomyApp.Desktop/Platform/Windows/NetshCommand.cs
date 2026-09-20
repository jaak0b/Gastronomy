using System.Diagnostics;
using System.Runtime.Versioning;
using GastronomyApp.Desktop.Values;
using GastronomyApp.Desktop.Ports;

namespace GastronomyApp.Desktop.Platform.Windows;

[SupportedOSPlatform("windows")]
public sealed class NetshCommand : INetshCommand
{
  public NetshResult Run(string arguments)
  {
    ProcessStartInfo startInfo = new()
                                 {
                                   FileName = "netsh",
                                   Arguments = arguments,
                                   UseShellExecute = false,
                                   CreateNoWindow = true,
                                   RedirectStandardOutput = true,
                                   RedirectStandardError = true
                                 };

    using var process = Process.Start(startInfo);
    if (process is null)
      throw new InvalidOperationException("The netsh command could not be started.");

    Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
    Task<string> standardError = process.StandardError.ReadToEndAsync();

    Task.WaitAll(standardOutput, standardError);
    process.WaitForExit();

    return new(process.ExitCode, standardError.Result);
  }
}
