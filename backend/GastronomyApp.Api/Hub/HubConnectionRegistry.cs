using System.Collections.Concurrent;
using GastronomyApp.Api.Values;

namespace GastronomyApp.Api.Hub;

public sealed class HubConnectionRegistry
{
  private readonly ConcurrentDictionary<string, TrackedHubConnection> _connections = new();

  public void Add(TrackedHubConnection connection)
  {
    ArgumentNullException.ThrowIfNull(connection);

    _connections[connection.ConnectionId] = connection;
  }

  public void Remove(string connectionId)
  {
    _connections.TryRemove(connectionId, out _);
  }

  public IReadOnlyList<TrackedHubConnection> FindByDevice(Guid deviceId)
  {
    return _connections.Values.Where(connection => connection.DeviceId == deviceId).ToList();
  }
}
