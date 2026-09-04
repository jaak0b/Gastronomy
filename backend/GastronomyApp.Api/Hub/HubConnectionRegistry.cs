using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace GastronomyApp.Api.Hub;

public sealed record TrackedHubConnection
{
  public required string ConnectionId { get; init; }

  public required Guid? DeviceId { get; init; }

  public required IReadOnlyList<string> Groups { get; init; }

  public required HubCallerContext CallerContext { get; init; }
}

public sealed class HubConnectionRegistry
{
  private readonly ConcurrentDictionary<string, TrackedHubConnection> _connections = new();

  public void Add(TrackedHubConnection connection)
  {
    _connections[connection.ConnectionId] = connection;
  }

  public void Remove(string connectionId)
  {
    _connections.TryRemove(connectionId, out _);
  }

  public IReadOnlyList<TrackedHubConnection> FindByDevice(Guid deviceId)
  {
    return [.. _connections.Values.Where(connection => connection.DeviceId == deviceId)];
  }
}

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
