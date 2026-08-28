using GastronomyApp.Api.Auth;
using Microsoft.AspNetCore.SignalR;

namespace GastronomyApp.Api.Hub;

public sealed record HubGroupNames
{
  public string Devices { get; } = "devices";

  public string Admin { get; } = "admin";

  public string StaffMember(Guid staffMemberId)
  {
    return $"staffMember:{staffMemberId}";
  }

  public string Device(Guid deviceId)
  {
    return $"device:{deviceId}";
  }
}

public sealed record HubEventNames
{
  public string OrderAccepted { get; } = "OrderAccepted";

  public string PrintJobStatusChanged { get; } = "PrintJobStatusChanged";

  public string OrderStatusChanged { get; } = "OrderStatusChanged";

  public string StationBacklogChanged { get; } = "StationBacklogChanged";

  public string PrinterStatusChanged { get; } = "PrinterStatusChanged";

  public string CatalogChanged { get; } = "CatalogChanged";

  public string EnrolmentCompleted { get; } = "EnrolmentCompleted";

  public string DeviceRevoked { get; } = "DeviceRevoked";
}

public sealed class GastronomyHub : Microsoft.AspNetCore.SignalR.Hub
{
  private readonly CallerIdentity callerIdentity;
  private readonly HubConnectionRegistry connectionRegistry;
  private readonly HubGroupNames groupNames = new();
  private readonly LocalAddressSet localAddresses;

  public GastronomyHub(CallerIdentity callerIdentity,
                       LocalAddressSet localAddresses,
                       HubConnectionRegistry connectionRegistry)
  {
    this.callerIdentity = callerIdentity;
    this.localAddresses = localAddresses;
    this.connectionRegistry = connectionRegistry;
  }

  override public async Task OnConnectedAsync()
  {
    var caller = Context.User is null ? null : callerIdentity.ReadDevice(Context.User);
    var httpContext = Context.GetHttpContext();

    List<string> joinedGroups = [];

    if (caller is not null)
    {
      joinedGroups.Add(groupNames.StaffMember(caller.StaffMemberId));
      joinedGroups.Add(groupNames.Device(caller.DeviceId));
      joinedGroups.Add(groupNames.Devices);
    }

    if (caller is null
        && httpContext is not null
        && localAddresses.Contains(httpContext.Connection.RemoteIpAddress))
    {
      joinedGroups.Add(groupNames.Admin);
    }

    foreach (var group in joinedGroups)
    {
      await Groups.AddToGroupAsync(Context.ConnectionId, group);
    }

    connectionRegistry.Add(new()
                           {
                             ConnectionId = Context.ConnectionId,
                             DeviceId = caller?.DeviceId,
                             Groups = joinedGroups,
                             CallerContext = Context
                           });

    await base.OnConnectedAsync();
  }

  override public async Task OnDisconnectedAsync(Exception? exception)
  {
    connectionRegistry.Remove(Context.ConnectionId);

    await base.OnDisconnectedAsync(exception);
  }
}
