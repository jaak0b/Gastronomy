using System.Text.Json;
using GastronomyApp.Desktop.Values;
using GastronomyApp.Desktop.Ports;

namespace GastronomyApp.Desktop.Settings;

public sealed class SettingsStore : ISettingsStore
{
  private const string SettingsFileName = "settings.json";

  private readonly JsonSerializerOptions _serializerOptions = new()
                                                              {
                                                                PropertyNameCaseInsensitive = true,
                                                                WriteIndented = true
                                                              };

  private readonly string _settingsDirectory;

  public SettingsStore(string settingsDirectory)
  {
    _settingsDirectory = settingsDirectory;
  }

  public DesktopSettings Load()
  {
    var stored = ReadStoredSettings();

    return new(stored.Port, stored.DataDirectory ?? _settingsDirectory, stored.SelectedNetworkInterface, stored.Language, stored.LastUpdateCheckUtc);
  }

  public void Save(DesktopSettings settings)
  {
    Directory.CreateDirectory(_settingsDirectory);

    StoredSettings stored = new()
                            {
                              Port = settings.Port,
                              DataDirectory = settings.DataDirectory,
                              SelectedNetworkInterface = settings.SelectedNetworkInterface,
                              Language = settings.Language,
                              LastUpdateCheckUtc = settings.LastUpdateCheckUtc
                            };

    File.WriteAllText(Path.Combine(_settingsDirectory, SettingsFileName), JsonSerializer.Serialize(stored, _serializerOptions));
  }

  private StoredSettings ReadStoredSettings()
  {
    var path = Path.Combine(_settingsDirectory, SettingsFileName);
    if (!File.Exists(path))
      return new();

    return JsonSerializer.Deserialize<StoredSettings>(File.ReadAllText(path), _serializerOptions) ?? new StoredSettings();
  }
}
