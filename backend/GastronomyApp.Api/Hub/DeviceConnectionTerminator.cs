using Microsoft.AspNetCore.SignalR;

namespace GastronomyApp.Api.Hub;

public sealed class DeviceConnectionTerminator
{
  private readonly IHubContext<GastronomyHub> _hubContext;
  private readonly HubConnectionRegistry _registry;

  public DeviceConnectionTerminator(HubConnectionRegistry registry, IHubContext<GastronomyHub> hubContext)
  {
    _registry = registry;
    _hubContext = hubContext;
  }

  public async Task TerminateAsync(Guid deviceId, CancellationToken cancellationToken)
  {
    foreach (var connection in _registry.FindByDevice(deviceId))
    {
      foreach (var group in connection.Groups)
      {
        await _hubContext.Groups.RemoveFromGroupAsync(connection.ConnectionId, group, cancellationToken);
      }

      _registry.Remove(connection.ConnectionId);
      connection.CallerContext.Abort();
    }
  }
}
