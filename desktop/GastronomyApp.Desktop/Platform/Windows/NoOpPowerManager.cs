using GastronomyApp.Desktop.Ports;

namespace GastronomyApp.Desktop.Platform.Windows;

public sealed class NoOpPowerManager : IPowerManager
{
  public void PreventSleep()
  {
  }

  public void AllowSleep()
  {
  }
}
