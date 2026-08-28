using System.Net.Sockets;
using GastronomyApp.Api;
using GastronomyApp.Api.Options;
using Microsoft.AspNetCore.Builder;

namespace GastronomyApp.Desktop.Services;

public sealed class HostLauncher : IHostLauncher
{
  private readonly Func<string, IDataFolderSetup> dataFolderSetupFactory;
  private readonly INetworkAddressProvider networkAddressProvider;

  public HostLauncher(INetworkAddressProvider networkAddressProvider,
                      Func<string, IDataFolderSetup> dataFolderSetupFactory)
  {
    this.networkAddressProvider = networkAddressProvider;
    this.dataFolderSetupFactory = dataFolderSetupFactory;
  }

  public WebApplication? Application { get; private set; }

  public bool IsRunning => Application is not null;

  public async Task<HostLaunchResult> StartAsync(ApiHostOptions options,
                                                 CancellationToken cancellationToken = default)
  {
    if (networkAddressProvider.GetAvailableAddresses().Count == 0)
    {
      return new HostLaunchResult.NoNetworkAvailable();
    }

    var configuredFolder = dataFolderSetupFactory(options.DataDirectory);

    if (!configuredFolder.Exists() || !configuredFolder.CurrentUserCanWrite())
    {
      return new HostLaunchResult.DataFolderNotWritable(options.DataDirectory);
    }

    WebApplication? built = null;

    try
    {
      built = new GastronomyAppApiApplication().Build(options);
      await built.StartAsync(cancellationToken);
    }
    catch (Exception failure)
    {
      if (built is not null)
      {
        await built.DisposeAsync();
      }

      if (failure is IOException bindFailure && IsPortAlreadyBound(bindFailure))
      {
        return new HostLaunchResult.PortInUse(options.Port);
      }

      if (failure is UnauthorizedAccessException)
      {
        return new HostLaunchResult.DataFolderNotWritable(options.DataDirectory);
      }

      return new HostLaunchResult.StartFailed(failure);
    }

    Application = built;

    return new HostLaunchResult.Started(built);
  }

  public async Task StopAsync(CancellationToken cancellationToken = default)
  {
    if (Application is null)
    {
      return;
    }

    await Application.StopAsync(cancellationToken);
    await Application.DisposeAsync();
    Application = null;
  }

  private bool IsPortAlreadyBound(Exception failure)
  {
    for (var candidate = failure; candidate is not null; candidate = candidate.InnerException)
    {
      if (candidate is SocketException)
      {
        return true;
      }
    }

    return false;
  }
}
