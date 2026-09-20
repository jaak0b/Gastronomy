namespace GastronomyApp.Desktop.Services.Windows;

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
