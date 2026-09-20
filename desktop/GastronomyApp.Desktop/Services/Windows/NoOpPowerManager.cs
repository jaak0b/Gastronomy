namespace GastronomyApp.Desktop.Services.Windows;

public sealed class NoOpPowerManager : IPowerManager
{
  public void PreventSleep()
  {
  }

  public void AllowSleep()
  {
  }
}
