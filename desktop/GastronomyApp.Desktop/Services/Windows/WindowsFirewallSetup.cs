using System.Diagnostics;
using System.Runtime.Versioning;

namespace GastronomyApp.Desktop.Services.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsFirewallSetup : IFirewallSetup
{
  private const string RuleName = "GastronomyApp ordering system";

  private readonly string executablePath;

  public WindowsFirewallSetup(string executablePath)
  {
    this.executablePath = executablePath;
  }

  public bool IsRuleConfigured()
  {
    return RunNetsh($"advfirewall firewall show rule name=\"{RuleName}\"") == 0;
  }

  public void EnsureRuleConfigured()
  {
    var ruleSettings =
      $"action=allow program=\"{executablePath}\" "
      + "protocol=TCP profile=private remoteip=localsubnet enable=yes";

    var exitCode = IsRuleConfigured()
                     ? RunNetsh($"advfirewall firewall set rule name=\"{RuleName}\" dir=in new {ruleSettings}")
                     : RunNetsh($"advfirewall firewall add rule name=\"{RuleName}\" dir=in {ruleSettings}");

    if (exitCode != 0)
    {
      throw new InvalidOperationException($"Configuring the inbound firewall rule failed with exit code {exitCode}.");
    }
  }

  private int RunNetsh(string arguments)
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

    process.WaitForExit();

    return process.ExitCode;
  }
}

public sealed class NoFirewallSetup : IFirewallSetup
{
  public bool IsRuleConfigured()
  {
    return true;
  }

  public void EnsureRuleConfigured()
  {
  }
}
