using System.Reflection;
using GastronomyApp.Desktop.Localization;
using GastronomyApp.Desktop.Services.Windows;
using GastronomyApp.Desktop.ViewModels;
using GastronomyApp.Infrastructure;

namespace GastronomyApp.Desktop.Services;

public sealed class DesktopComposition : IDisposable
{
  private const string ProductFolderName = "GastronomyApp";

  private bool _disposed;

  public DesktopComposition()
  {
    ExecutablePath = Environment.ProcessPath ?? AppContext.BaseDirectory;
    DataDirectoryPath = ResolveDataDirectory();
    Version = ResolveVersion();

    Text = new DesktopTextProvider();
    DataFolderSetup = new WindowsDataFolderSetup(DataDirectoryPath);
    SettingsStore = new SettingsStore(DataDirectoryPath);
    FreePorts = new FreePortProvider();
    NetworkAddressProvider = new NetworkAddressProvider();
    HostLauncher = new(NetworkAddressProvider, path => new WindowsDataFolderSetup(path));
    SingleInstance = new SingleInstanceCoordinator();

    PowerManager = new NoOpPowerManager();
    FirewallSetup = new NoFirewallSetup();
    ElevatedSetupLauncher = new UnavailableElevatedSetupLauncher();

    if (OperatingSystem.IsWindows())
    {
      PowerManager = new WindowsPowerManager();
      FirewallSetup = new WindowsFirewallSetup(ExecutablePath, new NetshCommand());
      ElevatedSetupLauncher = new WindowsElevatedSetupLauncher(ExecutablePath);
    }

    ElevatedSetupSteps = new(FirewallSetup, DataFolderSetup);

    UpdateInstaller = new VelopackUpdateInstaller();
    FestivalReader = new HostFestivalReader(HostLauncher);
    UpdateInstallGate = new(FestivalReader, new(), new SystemClock());
    UpdateOnQuit = new(UpdateInstaller, UpdateInstallGate);
    AutomaticUpdateChecker = new(UpdateInstaller, SettingsStore);
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
    return new(HostLauncher, PowerManager, SettingsStore, Text, FreePorts, UpdateInstaller, Version);
  }

  public FirstRunViewModel CreateFirstRunViewModel()
  {
    return new(FirewallSetup, DataFolderSetup, ElevatedSetupLauncher, Text);
  }

  public QuitConfirmViewModel CreateQuitConfirmViewModel(MainWindowViewModel mainWindowViewModel, Action requestApplicationExit)
  {
    return new(mainWindowViewModel.StopAsync, Text, requestApplicationExit, UpdateOnQuit.PrepareAsync);
  }

  public UpdateConfirmViewModel CreateUpdateConfirmViewModel(string version)
  {
    return new(version, Text);
  }

  private string ResolveVersion()
  {
    var informational = typeof(DesktopComposition).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

    if (informational is null)
      return "0.0.0";

    var buildMetadataIndex = informational.IndexOf('+', StringComparison.Ordinal);

    if (buildMetadataIndex < 0)
      return informational;

    return informational[..buildMetadataIndex];
  }

  private string ResolveDataDirectory()
  {
    var root = Environment.SpecialFolder.LocalApplicationData;

    if (OperatingSystem.IsWindows())
      root = Environment.SpecialFolder.CommonApplicationData;

    return Path.Combine(Environment.GetFolderPath(root), ProductFolderName);
  }

  public void Dispose()
  {
    Dispose(true);
    GC.SuppressFinalize(this);
  }

  private void Dispose(bool disposing)
  {
    if (_disposed)
      return;

    if (disposing && UpdateInstaller is IDisposable disposableUpdateInstaller)
      disposableUpdateInstaller.Dispose();

    _disposed = true;
  }
}
