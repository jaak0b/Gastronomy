using GastronomyApp.Desktop.Values;

namespace GastronomyApp.Desktop.Ports;

public interface ISettingsStore
{
  public DesktopSettings Load();

  public void Save(DesktopSettings settings);
}
