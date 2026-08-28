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

  private readonly string settingsDirectory;
  private readonly JsonSerializerOptions serializerOptions = new()
  {
    PropertyNameCaseInsensitive = true,
    WriteIndented = true,
  };

  public SettingsStore(string settingsDirectory)
  {
    this.settingsDirectory = settingsDirectory;
  }

  public DesktopSettings Load()
  {
    StoredSettings stored = ReadStoredSettings();

    return new DesktopSettings(
        stored.Port,
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
      Language = settings.Language,
    };

    File.WriteAllText(
        Path.Combine(settingsDirectory, SettingsFileName),
        JsonSerializer.Serialize(stored, serializerOptions));
  }

  private StoredSettings ReadStoredSettings()
  {
    string path = Path.Combine(settingsDirectory, SettingsFileName);
    if (!File.Exists(path))
    {
      return new StoredSettings();
    }

    return JsonSerializer.Deserialize<StoredSettings>(File.ReadAllText(path), serializerOptions)
        ?? new StoredSettings();
  }
}
