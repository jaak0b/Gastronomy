using GastronomyApp.Desktop.Services;

namespace GastronomyApp.Desktop.Tests.Services;

[TestFixture]
public sealed class SettingsStoreTests
{
    private string _root = null!;
    private string _settingsDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), $"gastronomy-settings-{Guid.NewGuid():N}");
        _settingsDirectory = Path.Combine(_root, "data");
        Directory.CreateDirectory(_settingsDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        Directory.Delete(_root, recursive: true);
    }

    private SettingsStore CreateStore()
    {
        return new SettingsStore(_settingsDirectory);
    }

    [Test]
    public void Load_WithoutASettingsFile_HasNoPortWrittenDownYet()
    {
        DesktopSettings settings = CreateStore().Load();

        Assert.Multiple(() =>
        {
            Assert.That(settings.Port, Is.Null);
            Assert.That(settings.DataDirectory, Is.EqualTo(_settingsDirectory));
            Assert.That(settings.SelectedNetworkInterface, Is.Null);
        });
    }

    [Test]
    public void Load_WithASettingsFile_ReadsThePortThatWasWrittenDown()
    {
        File.WriteAllText(
            Path.Combine(_settingsDirectory, "settings.json"),
            """{"Port":8080,"SelectedNetworkInterface":"Festival"}""");

        DesktopSettings settings = CreateStore().Load();

        Assert.Multiple(() =>
        {
            Assert.That(settings.Port, Is.EqualTo(8080));
            Assert.That(settings.SelectedNetworkInterface, Is.EqualTo("Festival"));
        });
    }

    [Test]
    public void Save_WritesSettingsJsonThatLoadReadsBackUnchanged()
    {
        SettingsStore store = CreateStore();

        store.Save(new DesktopSettings(8080, _settingsDirectory, "Festival", null));

        DesktopSettings reloaded = CreateStore().Load();

        Assert.That(reloaded, Is.EqualTo(new DesktopSettings(8080, _settingsDirectory, "Festival", null)));
    }
}
