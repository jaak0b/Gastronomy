using GastronomyApp.Desktop.Values;
using GastronomyApp.Api.Values;

namespace GastronomyApp.Desktop.Ports;

public interface IHostLauncher
{
  public bool IsRunning { get; }

  public Task<HostLaunchResult> StartAsync(ApiHostOptions options, CancellationToken cancellationToken = default);

  public Task StopAsync(CancellationToken cancellationToken = default);
}
