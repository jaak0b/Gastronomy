using GastronomyApp.Desktop.Localization;
using GastronomyApp.Desktop.Services.Windows;
using GastronomyApp.Desktop.ViewModels;

namespace GastronomyApp.Desktop.Services;

public sealed class DesktopComposition
{
  private const string ProductFolderName = "GastronomyApp";

  public DesktopComposition()
  {
    ExecutablePath = Environment.ProcessPath ?? AppContext.BaseDirectory;
    DataDirectoryPath = ResolveDataDirectory();

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
                      ? new WindowsFirewallSetup(ExecutablePath)
                      : new NoFirewallSetup();

    ElevatedSetupLauncher = OperatingSystem.IsWindows()
                              ? new WindowsElevatedSetupLauncher(ExecutablePath)
                              : new UnavailableElevatedSetupLauncher();
  }

  public string ExecutablePath { get; }

  public string DataDirectoryPath { get; }

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

  public MainWindowViewModel CreateMainWindowViewModel()
  {
    return new(HostLauncher,
               PowerManager,
               SettingsStore,
               Text,
               FreePorts);
  }

  public FirstRunViewModel CreateFirstRunViewModel()
  {
    return new(FirewallSetup, DataFolderSetup, ElevatedSetupLauncher, Text);
  }

  public QuitConfirmViewModel CreateQuitConfirmViewModel(MainWindowViewModel mainWindowViewModel,
                                                         Action requestApplicationExit)
  {
    return new(mainWindowViewModel.StopAsync, Text, requestApplicationExit);
  }

  public void RunElevatedSetupSteps()
  {
    FirewallSetup.EnsureRuleConfigured();

    if (DataFolderSetup.Exists())
    {
      DataFolderSetup.GrantUsersModifyOnExisting();

      return;
    }

    DataFolderSetup.CreateWithUsersModifyGrant();
  }

  private string ResolveDataDirectory()
  {
    var root = OperatingSystem.IsWindows()
                 ? Environment.SpecialFolder.CommonApplicationData
                 : Environment.SpecialFolder.LocalApplicationData;

    return Path.Combine(Environment.GetFolderPath(root), ProductFolderName);
  }
}
