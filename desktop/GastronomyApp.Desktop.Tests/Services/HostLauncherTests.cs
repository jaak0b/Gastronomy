using FakeItEasy;
using GastronomyApp.Api.Options;
using GastronomyApp.Desktop.Services;

namespace GastronomyApp.Desktop.Tests.Services;

[TestFixture]
public sealed class HostLauncherTests
{
  private const string ConfiguredFolder = @"D:\Festival\GastronomyApp";

  private INetworkAddressProvider _networkAddressProvider = null!;
  private IDataFolderSetup _dataFolderSetup = null!;
  private readonly List<string> _foldersAskedAbout = [];

  [SetUp]
  public void SetUp()
  {
    _foldersAskedAbout.Clear();
    _networkAddressProvider = A.Fake<INetworkAddressProvider>();
    _dataFolderSetup = A.Fake<IDataFolderSetup>();

    A.CallTo(() => _networkAddressProvider.GetAvailableAddresses())
        .Returns(new List<NetworkAddressOption> { new("WiFi", "192.168.1.20") });
    A.CallTo(() => _dataFolderSetup.Exists()).Returns(true);
    A.CallTo(() => _dataFolderSetup.CurrentUserCanWrite()).Returns(true);
  }

  private HostLauncher CreateLauncher()
  {
    return new HostLauncher(
        _networkAddressProvider,
        path =>
        {
          _foldersAskedAbout.Add(path);

          return _dataFolderSetup;
        });
  }

  private ApiHostOptions ConfiguredOptions()
  {
    return new ApiHostOptions
    {
      DataDirectory = ConfiguredFolder,
      Port = 5000,
      BindAddress = "0.0.0.0",
    };
  }

  [Test]
  public async Task StartAsync_WithNoNetworkAdapterUp_ReportsNoNetworkWithoutTouchingTheFolder()
  {
    A.CallTo(() => _networkAddressProvider.GetAvailableAddresses())
        .Returns(new List<NetworkAddressOption>());

    HostLaunchResult result = await CreateLauncher().StartAsync(ConfiguredOptions());

    Assert.Multiple(() =>
    {
      Assert.That(result, Is.InstanceOf<HostLaunchResult.NoNetworkAvailable>());
      Assert.That(_foldersAskedAbout, Is.Empty);
    });
  }

  [Test]
  public async Task StartAsync_ChecksTheFolderTheOptionsName_NotTheDefaultOne()
  {
    A.CallTo(() => _dataFolderSetup.CurrentUserCanWrite()).Returns(false);

    HostLaunchResult result = await CreateLauncher().StartAsync(ConfiguredOptions());

    Assert.Multiple(() =>
    {
      Assert.That(_foldersAskedAbout, Is.EqualTo(new List<string> { ConfiguredFolder }));
      Assert.That(result, Is.InstanceOf<HostLaunchResult.DataFolderNotWritable>());
      Assert.That(
              ((HostLaunchResult.DataFolderNotWritable)result).Path,
              Is.EqualTo(ConfiguredFolder));
    });
  }

  [Test]
  public async Task StartAsync_WhenTheFolderIsMissing_ReportsItAsNotWritable()
  {
    A.CallTo(() => _dataFolderSetup.Exists()).Returns(false);

    HostLaunchResult result = await CreateLauncher().StartAsync(ConfiguredOptions());

    Assert.That(result, Is.InstanceOf<HostLaunchResult.DataFolderNotWritable>());
  }

  [Test]
  public async Task StopAsync_WithNothingRunning_DoesNothingAndStaysStopped()
  {
    HostLauncher launcher = CreateLauncher();

    await launcher.StopAsync();

    Assert.That(launcher.IsRunning, Is.False);
  }
}
