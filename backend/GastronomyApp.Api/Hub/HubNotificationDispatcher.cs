using GastronomyApp.Api.Names;
using GastronomyApp.Contracts;
using GastronomyApp.Contracts.Enums;
using Microsoft.AspNetCore.SignalR;

namespace GastronomyApp.Api.Hub;

public sealed class HubNotificationDispatcher
{
  private readonly HubEventNames _eventNames = new();
  private readonly HubGroupNames _groupNames = new();
  private readonly IHubContext<GastronomyHub> _hubContext;

  public HubNotificationDispatcher(IHubContext<GastronomyHub> hubContext)
  {
    _hubContext = hubContext;
  }

  public async Task PushOrderStatusChangedAsync(Guid orderId, OrderStatus newStatus, CancellationToken ct)
  {
    await SendToAsync(_eventNames.OrderStatusChanged,
                      new OrderStatusChangedEvent(orderId, newStatus),
                      [
                        _groupNames.Devices,
                        _groupNames.Admin
                      ],
                      ct);
  }

  public async Task PushStationOrdersChangedAsync(Guid stationId, CancellationToken ct)
  {
    await SendToAsync(_eventNames.StationOrdersChanged,
                      new StationOrdersChangedEvent(stationId),
                      [
                        _groupNames.Devices,
                        _groupNames.BuildStationGroupName(stationId),
                        _groupNames.Admin
                      ],
                      ct);
  }

  public async Task PushStationsChangedAsync(Guid stationId, CancellationToken ct)
  {
    await SendToAsync(_eventNames.StationsChanged,
                      new StationsChangedEvent(),
                      [
                        _groupNames.Devices,
                        _groupNames.Admin,
                        _groupNames.BuildStationGroupName(stationId)
                      ],
                      ct);
  }

  public async Task PushOrderItemsSettledAsync(OrderItemsSettledEvent payload, CancellationToken ct)
  {
    await SendToAsync(_eventNames.OrderItemsSettled,
                      payload,
                      [
                        _groupNames.Devices,
                        _groupNames.Admin
                      ],
                      ct);
  }

  public async Task PushFestivalChangedAsync(CancellationToken ct)
  {
    await SendToAsync(_eventNames.FestivalChanged,
                      new FestivalChangedEvent(),
                      [
                        _groupNames.Devices,
                        _groupNames.Stations,
                        _groupNames.Admin
                      ],
                      ct);
  }

  public async Task PushCatalogChangedAsync(CancellationToken ct)
  {
    await SendToAsync(_eventNames.CatalogChanged,
                      new CatalogChangedEvent(),
                      [
                        _groupNames.Devices,
                        _groupNames.Admin
                      ],
                      ct);
  }

  public async Task PushEnrolmentCompletedAsync(EnrolmentCompletedEvent payload, CancellationToken ct)
  {
    await SendToAsync(_eventNames.EnrolmentCompleted, payload, [_groupNames.Admin], ct);
  }

  public async Task PushDeviceRevokedAsync(Guid deviceId, CancellationToken ct)
  {
    await SendToAsync(_eventNames.DeviceRevoked,
                      new DeviceRevokedEvent(deviceId),
                      [
                        _groupNames.BuildDeviceGroupName(deviceId),
                        _groupNames.Admin
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
