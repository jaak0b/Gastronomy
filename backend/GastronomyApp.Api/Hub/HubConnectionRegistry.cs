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
    private readonly ConcurrentDictionary<string, TrackedHubConnection> connections = new();

    public void Add(TrackedHubConnection connection)
    {
        connections[connection.ConnectionId] = connection;
    }

    public void Remove(string connectionId)
    {
        connections.TryRemove(connectionId, out _);
    }

    public IReadOnlyList<TrackedHubConnection> FindByDevice(Guid deviceId)
    {
        return [.. connections.Values.Where(connection => connection.DeviceId == deviceId)];
    }
}

public sealed class DeviceConnectionTerminator
{
    private readonly HubConnectionRegistry registry;
    private readonly IHubContext<GastronomyHub> hubContext;

    public DeviceConnectionTerminator(HubConnectionRegistry registry, IHubContext<GastronomyHub> hubContext)
    {
        this.registry = registry;
        this.hubContext = hubContext;
    }

    public async Task TerminateAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        foreach (TrackedHubConnection connection in registry.FindByDevice(deviceId))
        {
            foreach (string group in connection.Groups)
            {
                await hubContext.Groups.RemoveFromGroupAsync(connection.ConnectionId, group, cancellationToken);
            }

            registry.Remove(connection.ConnectionId);
            connection.CallerContext.Abort();
        }
    }
}
