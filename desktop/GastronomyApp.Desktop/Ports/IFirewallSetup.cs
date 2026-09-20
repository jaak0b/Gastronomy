namespace GastronomyApp.Desktop.Ports;

public interface IFirewallSetup
{
  public bool IsRuleConfigured();

  public void EnsureRuleConfigured();
}
