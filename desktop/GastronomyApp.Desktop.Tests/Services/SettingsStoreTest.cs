using GastronomyApp.Desktop.Services;

namespace GastronomyApp.Desktop.Tests.Services;

[TestFixture]
public sealed class SettingsStoreTests
{

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
    Directory.Delete(_root, true);
  }

  private string _root = null!;
  private string _settingsDirectory = null!;

  private SettingsStore CreateStore()
  {
    return new(_settingsDirectory);
  }

  [Test]
  public void Load_WithoutASettingsFile_HasNoPortWrittenDownYet()
  {
    var settings = CreateStore().Load();

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
    File.WriteAllText(Path.Combine(_settingsDirectory, "settings.json"),
                      """{"Port":8080,"SelectedNetworkInterface":"Festival"}""");

    var settings = CreateStore().Load();

    Assert.Multiple(() =>
                    {
                      Assert.That(settings.Port, Is.EqualTo(8080));
                      Assert.That(settings.SelectedNetworkInterface, Is.EqualTo("Festival"));
                    });
  }

  [Test]
  public void Save_WritesSettingsJsonThatLoadReadsBackUnchanged()
  {
    var store = CreateStore();

    store.Save(new(8080, _settingsDirectory, "Festival", null));

    var reloaded = CreateStore().Load();

    Assert.That(reloaded, Is.EqualTo(new DesktopSettings(8080, _settingsDirectory, "Festival", null)));
  }

  [Test]
  public void Save_WritesTheLastUpdateCheckMomentThatLoadReadsBackUnchanged()
  {
    var store = CreateStore();
    var checkedAt = new DateTimeOffset(2026, 9, 12, 18, 30, 0, TimeSpan.Zero);

    store.Save(new(8080, _settingsDirectory, "Festival", null, checkedAt));

    var reloaded = CreateStore().Load();

    Assert.That(reloaded.LastUpdateCheckUtc, Is.EqualTo(checkedAt));
  }
}
