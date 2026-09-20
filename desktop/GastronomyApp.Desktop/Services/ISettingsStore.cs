namespace GastronomyApp.Desktop.Services;

public interface ISettingsStore
{
  public DesktopSettings Load();

  public void Save(DesktopSettings settings);
}
