namespace GastronomyApp.Desktop.Settings;

public interface ISettingsStore
{
  public DesktopSettings Load();

  public void Save(DesktopSettings settings);
}
