using System.Text.Json;

namespace GastronomyApp.Desktop.Services;

public sealed record ShippedDefaults
{
    public int? Port { get; init; }

    public string? BindAddress { get; init; }
}

public sealed record StoredSettings
{
    public int? Port { get; init; }

    public string? BindAddress { get; init; }

    public string? DataDirectory { get; init; }

    public string? SelectedNetworkInterface { get; init; }
}

public sealed class SettingsStore : ISettingsStore
{
    private const string SettingsFileName = "settings.json";
    private const int FallbackPort = 5000;
    private const string FallbackBindAddress = "0.0.0.0";

    private readonly string shippedDefaultsPath;
    private readonly string settingsDirectory;
    private readonly JsonSerializerOptions serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public SettingsStore(string shippedDefaultsPath, string settingsDirectory)
    {
        this.shippedDefaultsPath = shippedDefaultsPath;
        this.settingsDirectory = settingsDirectory;
    }

    public DesktopSettings Load()
    {
        ShippedDefaults defaults = ReadShippedDefaults();
        StoredSettings stored = ReadStoredSettings();

        return new DesktopSettings(
            stored.Port ?? defaults.Port ?? FallbackPort,
            stored.BindAddress ?? defaults.BindAddress ?? FallbackBindAddress,
            stored.DataDirectory ?? settingsDirectory,
            stored.SelectedNetworkInterface);
    }

    public void Save(DesktopSettings settings)
    {
        Directory.CreateDirectory(settingsDirectory);

        StoredSettings stored = new()
        {
            Port = settings.Port,
            BindAddress = settings.BindAddress,
            DataDirectory = settings.DataDirectory,
            SelectedNetworkInterface = settings.SelectedNetworkInterface,
        };

        File.WriteAllText(
            Path.Combine(settingsDirectory, SettingsFileName),
            JsonSerializer.Serialize(stored, serializerOptions));
    }

    private ShippedDefaults ReadShippedDefaults()
    {
        if (!File.Exists(shippedDefaultsPath))
        {
            return new ShippedDefaults();
        }

        return JsonSerializer.Deserialize<ShippedDefaults>(File.ReadAllText(shippedDefaultsPath), serializerOptions)
            ?? new ShippedDefaults();
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
