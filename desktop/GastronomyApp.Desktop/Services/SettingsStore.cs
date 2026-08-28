using System.Text.Json;

namespace GastronomyApp.Desktop.Services;

public sealed record StoredSettings
{
  public int? Port { get; init; }

  public string? DataDirectory { get; init; }

  public string? SelectedNetworkInterface { get; init; }

  public string? Language { get; init; }
}

public sealed class SettingsStore : ISettingsStore
{
  private const string SettingsFileName = "settings.json";

  private readonly JsonSerializerOptions serializerOptions = new()
                                                             {
                                                               PropertyNameCaseInsensitive = true,
                                                               WriteIndented = true
                                                             };

  private readonly string settingsDirectory;

  public SettingsStore(string settingsDirectory)
  {
    this.settingsDirectory = settingsDirectory;
  }

  public DesktopSettings Load()
  {
    var stored = ReadStoredSettings();

    return new(stored.Port,
               stored.DataDirectory ?? settingsDirectory,
               stored.SelectedNetworkInterface,
               stored.Language);
  }

  public void Save(DesktopSettings settings)
  {
    Directory.CreateDirectory(settingsDirectory);

    StoredSettings stored = new()
                            {
                              Port = settings.Port,
                              DataDirectory = settings.DataDirectory,
                              SelectedNetworkInterface = settings.SelectedNetworkInterface,
                              Language = settings.Language
                            };

    File.WriteAllText(Path.Combine(settingsDirectory, SettingsFileName),
                      JsonSerializer.Serialize(stored, serializerOptions));
  }

  private StoredSettings ReadStoredSettings()
  {
    var path = Path.Combine(settingsDirectory, SettingsFileName);
    if (!File.Exists(path))
    {
      return new();
    }

    return JsonSerializer.Deserialize<StoredSettings>(File.ReadAllText(path), serializerOptions)
           ?? new StoredSettings();
  }
}
