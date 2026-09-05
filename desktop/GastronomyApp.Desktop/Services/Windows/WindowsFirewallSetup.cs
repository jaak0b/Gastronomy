using System.Runtime.Versioning;

namespace GastronomyApp.Desktop.Services.Windows;

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
    var ruleSettings =
      $"action=allow program=\"{_executablePath}\" "
      + $"protocol=TCP profile={CoveredProfiles} remoteip=localsubnet enable=yes";

    var result = RuleExists()
                   ? _netsh.Run($"advfirewall firewall set rule name=\"{RuleName}\" dir=in new {ruleSettings}")
                   : _netsh.Run($"advfirewall firewall add rule name=\"{RuleName}\" dir=in {ruleSettings}");

    if (result.ExitCode != 0)
    {
      throw new InvalidOperationException(DescribeFailure(result));
    }
  }

  private string DescribeFailure(NetshResult result)
  {
    var reportedProblem = result.ErrorOutput.Trim();

    return reportedProblem.Length == 0
             ? $"Configuring the inbound firewall rule failed with exit code {result.ExitCode}."
             : $"Configuring the inbound firewall rule failed with exit code {result.ExitCode}: {reportedProblem}";
  }

  private bool RuleExists()
  {
    return ShowRule(string.Empty).ExitCode == 0;
  }

  private NetshResult ShowRule(string profileFilter)
  {
    return _netsh.Run($"advfirewall firewall show rule name=\"{RuleName}\"{profileFilter}");
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
