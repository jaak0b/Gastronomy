using System.Text.Json;

namespace GastronomyApp.Desktop.Services;

public sealed record StoredSettings
{
  public int? Port { get; init; }

  public string? DataDirectory { get; init; }

  public string? SelectedNetworkInterface { get; init; }

  public string? Language { get; init; }

  public DateTimeOffset? LastUpdateCheckUtc { get; init; }
}

public sealed class SettingsStore : ISettingsStore
{
  private const string SettingsFileName = "settings.json";

  private readonly JsonSerializerOptions serializerOptions = new()
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

    return new(stored.Port,
               stored.DataDirectory ?? _settingsDirectory,
               stored.SelectedNetworkInterface,
               stored.Language,
               stored.LastUpdateCheckUtc);
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

    File.WriteAllText(Path.Combine(_settingsDirectory, SettingsFileName),
                      JsonSerializer.Serialize(stored, serializerOptions));
  }

  private StoredSettings ReadStoredSettings()
  {
    var path = Path.Combine(_settingsDirectory, SettingsFileName);
    if (!File.Exists(path))
    {
      return new();
    }

    return JsonSerializer.Deserialize<StoredSettings>(File.ReadAllText(path), serializerOptions)
           ?? new StoredSettings();
  }
}
