using System.Runtime.Versioning;
using GastronomyApp.Desktop.Values;
using GastronomyApp.Desktop.Ports;

namespace GastronomyApp.Desktop.Platform.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsFirewallSetup : IFirewallSetup
{
  private const string RuleName = "GastronomyApp ordering system";

  private const string CoveredProfiles = "private,public";

  private readonly string _executablePath;

  private readonly INetshCommand _netsh;

  public WindowsFirewallSetup(string executablePath, INetshCommand netsh)
  {
    _executablePath = executablePath;
    _netsh = netsh;
  }

  public bool IsRuleConfigured()
  {
    return ShowRule($" profile={CoveredProfiles}").ExitCode == 0;
  }

  public void EnsureRuleConfigured()
  {
    var ruleSettings = $"action=allow program=\"{_executablePath}\" " + $"protocol=TCP profile={CoveredProfiles} remoteip=localsubnet enable=yes";

    NetshResult result;

    if (ShowRule(string.Empty).ExitCode == 0)
      result = _netsh.Run($"advfirewall firewall set rule name=\"{RuleName}\" dir=in new {ruleSettings}");
    else
      result = _netsh.Run($"advfirewall firewall add rule name=\"{RuleName}\" dir=in {ruleSettings}");

    if (result.ExitCode != 0)
      throw new InvalidOperationException(BuildFailureText(result));
  }

  private string BuildFailureText(NetshResult result)
  {
    var reportedProblem = result.ErrorOutput.Trim();

    if (reportedProblem.Length == 0)
      return $"Configuring the inbound firewall rule failed with exit code {result.ExitCode}.";

    return $"Configuring the inbound firewall rule failed with exit code {result.ExitCode}: {reportedProblem}";
  }

  private NetshResult ShowRule(string profileFilter)
  {
    return _netsh.Run($"advfirewall firewall show rule name=\"{RuleName}\"{profileFilter}");
  }
}
