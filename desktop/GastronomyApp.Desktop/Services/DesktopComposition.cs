using System.Reflection;
using GastronomyApp.Core.Services;
using GastronomyApp.Desktop.Localization;
using GastronomyApp.Desktop.Services.Windows;
using GastronomyApp.Desktop.ViewModels;
using GastronomyApp.Infrastructure;

namespace GastronomyApp.Desktop.Services;

public sealed class DesktopComposition
{
  private const string ProductFolderName = "GastronomyApp";

  public DesktopComposition()
  {
    ExecutablePath = Environment.ProcessPath ?? AppContext.BaseDirectory;
    DataDirectoryPath = ResolveDataDirectory();
    Version = ResolveVersion();

    Text = new DesktopTextProvider();
    DataFolderSetup = new DataFolderSetup(DataDirectoryPath);
    SettingsStore = new SettingsStore(DataDirectoryPath);
    FreePorts = new FreePortProvider();
    NetworkAddressProvider = new NetworkAddressProvider();
    HostLauncher = new(NetworkAddressProvider, path => new DataFolderSetup(path));
    SingleInstance = new SingleInstanceCoordinator();

    PowerManager = OperatingSystem.IsWindows()
                     ? new WindowsPowerManager()
                     : new NoOpPowerManager();

    FirewallSetup = OperatingSystem.IsWindows()
                      ? new WindowsFirewallSetup(ExecutablePath, new NetshCommand())
                      : new NoFirewallSetup();

    ElevatedSetupLauncher = OperatingSystem.IsWindows()
                              ? new WindowsElevatedSetupLauncher(ExecutablePath)
                              : new UnavailableElevatedSetupLauncher();

    ElevatedSetupSteps = new(FirewallSetup, DataFolderSetup);

    UpdateInstaller = new VelopackUpdateInstaller();
    FestivalReader = new HostFestivalReader(HostLauncher);
    UpdateInstallGate = new UpdateInstallGate(FestivalReader, new FestivalSchedule(), new SystemClock());
    UpdateOnQuit = new UpdateOnQuit(UpdateInstaller, UpdateInstallGate);
    AutomaticUpdateChecker = new AutomaticUpdateChecker(UpdateInstaller, SettingsStore);
  }

  public string ExecutablePath { get; }

  public string DataDirectoryPath { get; }

  public string Version { get; }

  public IDesktopTextProvider Text { get; }

  public IDataFolderSetup DataFolderSetup { get; }

  public ISettingsStore SettingsStore { get; }

  public IFreePortProvider FreePorts { get; }

  public INetworkAddressProvider NetworkAddressProvider { get; }

  public HostLauncher HostLauncher { get; }

  public ISingleInstance SingleInstance { get; }

  public IPowerManager PowerManager { get; }

  public IFirewallSetup FirewallSetup { get; }

  public IElevatedSetupLauncher ElevatedSetupLauncher { get; }

  public ElevatedSetupSteps ElevatedSetupSteps { get; }

  public IUpdateInstaller UpdateInstaller { get; }

  public IFestivalReader FestivalReader { get; }

  public UpdateInstallGate UpdateInstallGate { get; }

  public UpdateOnQuit UpdateOnQuit { get; }

  public AutomaticUpdateChecker AutomaticUpdateChecker { get; }

  public MainWindowViewModel CreateMainWindowViewModel()
  {
    return new(HostLauncher,
               PowerManager,
               SettingsStore,
               Text,
               FreePorts,
               UpdateInstaller,
               Version);
  }

  public FirstRunViewModel CreateFirstRunViewModel()
  {
    return new(FirewallSetup, DataFolderSetup, ElevatedSetupLauncher, Text);
  }

  public QuitConfirmViewModel CreateQuitConfirmViewModel(MainWindowViewModel mainWindowViewModel,
                                                         Action requestApplicationExit)
  {
    return new(mainWindowViewModel.StopAsync, Text, requestApplicationExit, UpdateOnQuit.PrepareAsync);
  }

  public UpdateConfirmViewModel CreateUpdateConfirmViewModel(string version)
  {
    return new(version, Text);
  }

  private string ResolveVersion()
  {
    var informational = typeof(DesktopComposition).Assembly
                                                 .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                                                 ?.InformationalVersion;

    if (informational is null)
    {
      return "0.0.0";
    }

    var buildMetadataIndex = informational.IndexOf('+', StringComparison.Ordinal);

    return buildMetadataIndex < 0 ? informational : informational[..buildMetadataIndex];
  }

  private string ResolveDataDirectory()
  {
    var root = OperatingSystem.IsWindows()
                 ? Environment.SpecialFolder.CommonApplicationData
                 : Environment.SpecialFolder.LocalApplicationData;

    return Path.Combine(Environment.GetFolderPath(root), ProductFolderName);
  }
}
