using GastronomyApp.Api.Auth;
using GastronomyApp.Api.Values;
using GastronomyApp.Contracts.Enums;
using Microsoft.AspNetCore.SignalR;

namespace GastronomyApp.Api.Hub;

public sealed class GastronomyHub : Microsoft.AspNetCore.SignalR.Hub
{
  private readonly CallerIdentity _callerIdentity;
  private readonly HubConnectionRegistry _connectionRegistry;
  private readonly LocalAddressSet _localAddresses;

  public GastronomyHub(CallerIdentity callerIdentity, LocalAddressSet localAddresses, HubConnectionRegistry connectionRegistry)
  {
    _callerIdentity = callerIdentity;
    _localAddresses = localAddresses;
    _connectionRegistry = connectionRegistry;
  }

  override public async Task OnConnectedAsync()
  {
    DeviceCaller? caller = null;

    if (Context.User is not null)
      caller = _callerIdentity.ReadDevice(Context.User);

    var httpContext = Context.GetHttpContext();

    List<string> joinedGroups = [];

    if (caller is not null)
    {
      joinedGroups.Add(Names.HubGroups.BuildDeviceGroupName(caller.DeviceId));

      if (caller.OwnerKind == DeviceOwnerKind.Station)
      {
        joinedGroups.Add(Names.HubGroups.BuildStationGroupName(caller.OwnerId));
        joinedGroups.Add(Names.HubGroups.Stations);
      }
      else
        joinedGroups.Add(Names.HubGroups.Devices);
    }

    if (caller is null && httpContext is not null && _localAddresses.Contains(httpContext.Connection.RemoteIpAddress))
      joinedGroups.Add(Names.HubGroups.Admin);

    foreach (var group in joinedGroups)
      await Groups.AddToGroupAsync(Context.ConnectionId, group);

    _connectionRegistry.Add(new()
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
    _connectionRegistry.Remove(Context.ConnectionId);

    await base.OnDisconnectedAsync(exception);
  }
}
