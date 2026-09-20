namespace GastronomyApp.Desktop.Services;

public interface IFirewallSetup
{
  public bool IsRuleConfigured();

  public void EnsureRuleConfigured();
}
