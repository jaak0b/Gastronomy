using GastronomyApp.Api;
using GastronomyApp.Api.Options;
using Microsoft.AspNetCore.Builder;

namespace GastronomyApp.Desktop.Services;

public sealed class HostLauncher : IHostLauncher
{
  private readonly INetworkAddressProvider networkAddressProvider;
  private readonly Func<string, IDataFolderSetup> dataFolderSetupFactory;

  private WebApplication? application;

  public HostLauncher(
      INetworkAddressProvider networkAddressProvider,
      Func<string, IDataFolderSetup> dataFolderSetupFactory)
  {
    this.networkAddressProvider = networkAddressProvider;
    this.dataFolderSetupFactory = dataFolderSetupFactory;
  }

  public bool IsRunning => application is not null;

  public WebApplication? Application => application;

  public async Task<HostLaunchResult> StartAsync(
      ApiHostOptions options,
      CancellationToken cancellationToken = default)
  {
    if (networkAddressProvider.GetAvailableAddresses().Count == 0)
    {
      return new HostLaunchResult.NoNetworkAvailable();
    }

    IDataFolderSetup configuredFolder = dataFolderSetupFactory(options.DataDirectory);

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

    application = built;

    return new HostLaunchResult.Started(built);
  }

  public async Task StopAsync(CancellationToken cancellationToken = default)
  {
    if (application is null)
    {
      return;
    }

    await application.StopAsync(cancellationToken);
    await application.DisposeAsync();
    application = null;
  }

  private bool IsPortAlreadyBound(Exception failure)
  {
    for (Exception? candidate = failure; candidate is not null; candidate = candidate.InnerException)
    {
      if (candidate is System.Net.Sockets.SocketException)
      {
        return true;
      }
    }

    return false;
  }
}
