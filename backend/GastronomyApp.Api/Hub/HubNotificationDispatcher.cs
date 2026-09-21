using GastronomyApp.Contracts.Events;
using Microsoft.AspNetCore.SignalR;

namespace GastronomyApp.Api.Hub;

public sealed class HubNotificationDispatcher
{
  private readonly IHubContext<GastronomyHub> _hubContext;

  public HubNotificationDispatcher(IHubContext<GastronomyHub> hubContext)
  {
    _hubContext = hubContext;
  }

  public async Task PushOrderStatusChangedAsync(OrderStatusChangedEvent payload, CancellationToken ct)
  {
    await SendToAsync(Names.HubEvents.OrderStatusChanged,
                      payload,
                      [
                        Names.HubGroups.Devices,
                        Names.HubGroups.Admin
                      ],
                      ct);
  }

  public async Task PushStationOrdersChangedAsync(Guid stationId, CancellationToken ct)
  {
    await SendToAsync(Names.HubEvents.StationOrdersChanged,
                      new StationOrdersChangedEvent(stationId),
                      [
                        Names.HubGroups.Devices,
                        Names.HubGroups.BuildStationGroupName(stationId),
                        Names.HubGroups.Admin
                      ],
                      ct);
  }

  public async Task PushStationsChangedAsync(Guid stationId, CancellationToken ct)
  {
    await SendToAsync(Names.HubEvents.StationsChanged,
                      new StationsChangedEvent(),
                      [
                        Names.HubGroups.Devices,
                        Names.HubGroups.Admin,
                        Names.HubGroups.BuildStationGroupName(stationId)
                      ],
                      ct);
  }

  public async Task PushOrderItemsSettledAsync(OrderItemsSettledEvent payload, CancellationToken ct)
  {
    await SendToAsync(Names.HubEvents.OrderItemsSettled,
                      payload,
                      [
                        Names.HubGroups.Devices,
                        Names.HubGroups.Admin
                      ],
                      ct);
  }

  public async Task PushFestivalChangedAsync(CancellationToken ct)
  {
    await SendToAsync(Names.HubEvents.FestivalChanged,
                      new FestivalChangedEvent(),
                      [
                        Names.HubGroups.Devices,
                        Names.HubGroups.Stations,
                        Names.HubGroups.Admin
                      ],
                      ct);
  }

  public async Task PushCatalogChangedAsync(CancellationToken ct)
  {
    await SendToAsync(Names.HubEvents.CatalogChanged,
                      new CatalogChangedEvent(),
                      [
                        Names.HubGroups.Devices,
                        Names.HubGroups.Admin
                      ],
                      ct);
  }

  public async Task PushEnrolmentCompletedAsync(EnrolmentCompletedEvent payload, CancellationToken ct)
  {
    await SendToAsync(Names.HubEvents.EnrolmentCompleted, payload, [Names.HubGroups.Admin], ct);
  }

  public async Task PushDeviceRevokedAsync(Guid deviceId, CancellationToken ct)
  {
    await SendToAsync(Names.HubEvents.DeviceRevoked,
                      new DeviceRevokedEvent(deviceId),
                      [
                        Names.HubGroups.BuildDeviceGroupName(deviceId),
                        Names.HubGroups.Admin
                      ],
                      ct);
  }

  private async Task SendToAsync(string eventName, object payload, IReadOnlyList<string> groups, CancellationToken ct)
  {
    ct.ThrowIfCancellationRequested();

    foreach (var group in groups)
      await _hubContext.Clients.Group(group).SendAsync(eventName, payload, ct);
  }
}
