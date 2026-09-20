namespace GastronomyApp.Desktop.Services;

public interface IPowerManager
{
  public void PreventSleep();

  public void AllowSleep();
}
