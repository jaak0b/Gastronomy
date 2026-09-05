using System.Diagnostics;
using System.Runtime.Versioning;

namespace GastronomyApp.Desktop.Services.Windows;

public sealed record NetshResult(int ExitCode, string ErrorOutput);

public interface INetshCommand
{
  public NetshResult Run(string arguments);
}

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
    {
      throw new InvalidOperationException("The netsh command could not be started.");
    }

    var standardOutput = process.StandardOutput.ReadToEndAsync();
    var standardError = process.StandardError.ReadToEndAsync();

    Task.WaitAll(standardOutput, standardError);
    process.WaitForExit();

    return new(process.ExitCode, standardError.Result);
  }
}
