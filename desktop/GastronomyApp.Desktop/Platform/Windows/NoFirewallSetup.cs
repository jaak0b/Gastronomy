using GastronomyApp.Desktop.Setup;

namespace GastronomyApp.Desktop.Platform.Windows;

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
