using GastronomyApp.Api.Options;
using Microsoft.AspNetCore.Builder;

namespace GastronomyApp.Desktop.Services;

public interface IHostLauncher
{

  public bool IsRunning { get; }

  public Task<HostLaunchResult> StartAsync(ApiHostOptions options, CancellationToken cancellationToken = default);

  public Task StopAsync(CancellationToken cancellationToken = default);
}

public abstract record HostLaunchResult
{
  public sealed record Started(WebApplication Application) : HostLaunchResult;

  public sealed record PortInUse(int Port) : HostLaunchResult;

  public sealed record DataFolderNotWritable(string Path) : HostLaunchResult;

  public sealed record NoNetworkAvailable : HostLaunchResult;

  public sealed record StartFailed(Exception Failure) : HostLaunchResult;
}

public interface IPowerManager
{
  public void PreventSleep();

  public void AllowSleep();
}

public interface IFirewallSetup
{
  public bool IsRuleConfigured();

  public void EnsureRuleConfigured();
}

public interface IDataFolderSetup
{
  public string DataDirectoryPath { get; }

  public bool Exists();

  public bool CurrentUserCanWrite();

  public void CreateWithUsersModifyGrant();

  public void GrantUsersModifyOnExisting();
}

public interface ISingleInstance
{
  public SingleInstanceOutcome AcquireOrSignalExisting();

  public event Action? ActivationRequested;

  public void Release();
}

public enum SingleInstanceOutcome
{
  AcquiredPrimary,
  SignaledExistingAndShouldExit
}

public interface INetworkAddressProvider
{
  public IReadOnlyList<NetworkAddressOption> GetAvailableAddresses();
}

public sealed record NetworkAddressOption(string InterfaceName, string IPAddress);

public interface ISettingsStore
{
  public DesktopSettings Load();

  public void Save(DesktopSettings settings);
}

public sealed record DesktopSettings(
  int? Port,
  string DataDirectory,
  string? SelectedNetworkInterface,
  string? Language);

public interface IFreePortProvider
{
  public int Reserve();
}

public interface IElevatedSetupLauncher
{
  public Task<ElevatedSetupOutcome> RunElevatedSetupAsync(CancellationToken cancellationToken = default);
}

public enum ElevatedSetupOutcome
{
  Completed,
  ElevationDeclined
}

public sealed record LanguageOption(string Code, string Name);

public interface IDesktopTextProvider
{
  public event Action? LanguageChanged;

  public void UseLanguage(string? languageCode);

  public string Get(string key);

  public string Format(string key, params TextPlaceholder[] placeholders);
}

public sealed record TextPlaceholder(string Name, string Value);
