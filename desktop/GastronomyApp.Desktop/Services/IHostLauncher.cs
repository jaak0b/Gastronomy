using GastronomyApp.Api.Options;

namespace GastronomyApp.Desktop.Services;

public interface IHostLauncher
{
  public bool IsRunning { get; }

  public Task<HostLaunchResult> StartAsync(ApiHostOptions options, CancellationToken cancellationToken = default);

  public Task StopAsync(CancellationToken cancellationToken = default);
}
