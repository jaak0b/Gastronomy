namespace GastronomyApp.Desktop.Ports;

public interface IPowerManager
{
  public void PreventSleep();

  public void AllowSleep();
}
