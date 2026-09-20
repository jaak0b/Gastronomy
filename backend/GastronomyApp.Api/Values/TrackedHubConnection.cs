using Microsoft.AspNetCore.SignalR;

namespace GastronomyApp.Api.Values;

public sealed record TrackedHubConnection
{
  public required string ConnectionId { get; init; }

  public required Guid? DeviceId { get; init; }

  public required IReadOnlyList<string> Groups { get; init; }

  public required HubCallerContext CallerContext { get; init; }
}
