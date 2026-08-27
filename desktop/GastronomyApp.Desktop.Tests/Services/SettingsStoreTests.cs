using GastronomyApp.Desktop.Services;

namespace GastronomyApp.Desktop.Tests.Services;

[TestFixture]
public sealed class SettingsStoreTests
{
    private string _root = null!;
    private string _shippedDefaultsPath = null!;
    private string _settingsDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), $"gastronomy-settings-{Guid.NewGuid():N}");
        _settingsDirectory = Path.Combine(_root, "data");
        Directory.CreateDirectory(_settingsDirectory);
        _shippedDefaultsPath = Path.Combine(_root, "appsettings.json");
        File.WriteAllText(
            _shippedDefaultsPath,
            """{"Scheme":"http","Port":5000,"BindAddress":"0.0.0.0","LogLevel":"Information"}""");
    }

    [TearDown]
    public void TearDown()
    {
        Directory.Delete(_root, recursive: true);
    }

    private SettingsStore CreateStore()
    {
        return new SettingsStore(_shippedDefaultsPath, _settingsDirectory);
    }

    [Test]
    public void Load_WithoutASettingsFile_ReturnsTheShippedDefaults()
    {
        DesktopSettings settings = CreateStore().Load();

        Assert.Multiple(() =>
        {
            Assert.That(settings.Port, Is.EqualTo(5000));
            Assert.That(settings.BindAddress, Is.EqualTo("0.0.0.0"));
            Assert.That(settings.DataDirectory, Is.EqualTo(_settingsDirectory));
            Assert.That(settings.SelectedNetworkInterface, Is.Null);
        });
    }

    [Test]
    public void Load_WithASettingsFile_LayersItOverTheShippedDefaults()
    {
        File.WriteAllText(
            Path.Combine(_settingsDirectory, "settings.json"),
            """{"Port":8080,"SelectedNetworkInterface":"Festival"}""");

        DesktopSettings settings = CreateStore().Load();

        Assert.Multiple(() =>
        {
            Assert.That(settings.Port, Is.EqualTo(8080));
            Assert.That(settings.BindAddress, Is.EqualTo("0.0.0.0"));
            Assert.That(settings.SelectedNetworkInterface, Is.EqualTo("Festival"));
        });
    }

    [Test]
    public void Save_WritesSettingsJsonAndNeverTouchesTheShippedDefaults()
    {
        string shippedBefore = File.ReadAllText(_shippedDefaultsPath);
        SettingsStore store = CreateStore();

        store.Save(new DesktopSettings(8080, "127.0.0.1", _settingsDirectory, "Festival"));

        DesktopSettings reloaded = CreateStore().Load();

        Assert.Multiple(() =>
        {
            Assert.That(reloaded, Is.EqualTo(new DesktopSettings(8080, "127.0.0.1", _settingsDirectory, "Festival")));
            Assert.That(File.ReadAllText(_shippedDefaultsPath), Is.EqualTo(shippedBefore));
        });
    }
}
