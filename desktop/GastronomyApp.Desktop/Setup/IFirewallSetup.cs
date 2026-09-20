namespace GastronomyApp.Desktop.Setup;

public interface IFirewallSetup
{
  public bool IsRuleConfigured();

  public void EnsureRuleConfigured();
}
