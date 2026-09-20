namespace GastronomyApp.Desktop.Platform;

public interface IPowerManager
{
  public void PreventSleep();

  public void AllowSleep();
}
