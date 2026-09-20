using System.Net.Sockets;
using GastronomyApp.Api;
using GastronomyApp.Api.Options;
using GastronomyApp.Desktop.Setup;
using GastronomyApp.Desktop.Values;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace GastronomyApp.Desktop.Hosting;

public sealed class HostLauncher : IHostLauncher
{
  private const int ShutdownTimeoutSeconds = 3;

  private readonly Func<string, IDataFolderSetup> _dataFolderSetupFactory;
  private readonly INetworkAddressProvider _networkAddressProvider;

  public HostLauncher(INetworkAddressProvider networkAddressProvider, Func<string, IDataFolderSetup> dataFolderSetupFactory)
  {
    _networkAddressProvider = networkAddressProvider;
    _dataFolderSetupFactory = dataFolderSetupFactory;
  }

  public WebApplication? Application { get; private set; }

  public bool IsRunning => Application is not null;

  public async Task<HostLaunchResult> StartAsync(ApiHostOptions options, CancellationToken cancellationToken = default)
  {
    if (_networkAddressProvider.GetAvailableAddresses().Count == 0)
      return new HostLaunchResult.NoNetworkAvailable();

    var configuredFolder = _dataFolderSetupFactory(options.DataDirectory);

    if (!configuredFolder.Exists() || !configuredFolder.CurrentUserCanWrite())
      return new HostLaunchResult.DataFolderNotWritable(options.DataDirectory);

    WebApplication? built = null;

    try
    {
      built = new GastronomyAppApiApplication().Build(options);
      await built.StartAsync(cancellationToken);
    }
    catch (Exception failure)
    {
      if (built is not null)
        await built.DisposeAsync();

      if (failure is IOException bindFailure && IsPortAlreadyBound(bindFailure))
        return new HostLaunchResult.PortInUse(options.Port);

      if (failure is UnauthorizedAccessException)
        return new HostLaunchResult.DataFolderNotWritable(options.DataDirectory);

      return new HostLaunchResult.StartFailed(failure);
    }

    Application = built;

    return new HostLaunchResult.Started(built);
  }

  public async Task StopAsync(CancellationToken cancellationToken = default)
  {
    var running = Application;

    if (running is null)
      return;

    Log.Information("The server is being stopped.");

    try
    {
      await Task.Run(async () =>
                     {
                       try
                       {
                         await running.StopAsync(TimeSpan.FromSeconds(ShutdownTimeoutSeconds));
                       }
                       catch (OperationCanceledException)
                       {
                         Log.Warning("The server did not stop within {Seconds} seconds, so its open connections are being cut.", ShutdownTimeoutSeconds);
                       }

                       await running.DisposeAsync();
                     },
                     cancellationToken);
    } finally
    {
      Application = null;
    }

    Log.Information("The server has stopped.");
  }

  private bool IsPortAlreadyBound(Exception failure)
  {
    for (var candidate = failure; candidate is not null; candidate = candidate.InnerException)
    {
      if (candidate is SocketException)
        return true;
    }

    return false;
  }
}
