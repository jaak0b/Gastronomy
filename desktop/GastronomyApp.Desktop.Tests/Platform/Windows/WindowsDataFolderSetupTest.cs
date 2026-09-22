using GastronomyApp.Desktop.Platform.Windows;

namespace GastronomyApp.Desktop.Tests.Platform.Windows;

[TestFixture]
public sealed class WindowsDataFolderSetupTest
{
  [SetUp]
  public void SetUp()
  {
    _folderPath = Path.Combine(Path.GetTempPath(), $"GastronomyAppDataFolder-{Guid.NewGuid():N}");
  }

  [TearDown]
  public void TearDown()
  {
    if (Directory.Exists(_folderPath))
      Directory.Delete(_folderPath, true);
  }

  private string _folderPath = null!;

  [Test]
  public void CurrentUserCanWrite_TheFolderIsNotThere_IsFalse()
  {
    WindowsDataFolderSetup setup = new(_folderPath);

    Assert.That(setup.CurrentUserCanWrite(), Is.False);
  }

  [Test]
  public void CurrentUserCanWrite_AFolderTheUserMayWriteIn_IsTrue()
  {
    Directory.CreateDirectory(_folderPath);

    WindowsDataFolderSetup setup = new(_folderPath);

    Assert.That(setup.CurrentUserCanWrite(), Is.True);
  }
}
