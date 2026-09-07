using GastronomyApp.Api.Contracts;
using GastronomyApp.Core.Enums;
using GastronomyApp.Infrastructure;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Hub;

public sealed class HubNotificationDispatcher
{
  private readonly IDbContextFactory<GastronomyAppDbContext> _contextFactory;
  private readonly HubEventNames _eventNames = new();
  private readonly HubGroupNames _groupNames = new();
  private readonly IHubContext<GastronomyHub> _hubContext;

  public HubNotificationDispatcher(IHubContext<GastronomyHub> hubContext,
                                   IDbContextFactory<GastronomyAppDbContext> contextFactory)
  {
    _hubContext = hubContext;
    _contextFactory = contextFactory;
  }

  public async Task OnOrderStatusChangedAsync(Guid orderId, OrderStatus newStatus, CancellationToken ct)
  {
    await using var context = await _contextFactory.CreateDbContextAsync(ct);
    var orderExists = await context.Orders.AnyAsync(candidate => candidate.Id == orderId, ct);

    if (!orderExists)
    {
      return;
    }

    await SendToAsync(_eventNames.OrderStatusChanged,
                      new OrderStatusChangedEvent(orderId, newStatus),
                      [_groupNames.Devices, _groupNames.Admin],
                      ct);
  }

  public async Task PushOrderAcceptedAsync(OrderAcceptedEvent payload, CancellationToken ct)
  {
    ArgumentNullException.ThrowIfNull(payload);

    await SendToAsync(_eventNames.OrderAccepted, payload, [_groupNames.Admin], ct);
  }

  public async Task PushStationOrdersChangedAsync(Guid stationId, CancellationToken ct)
  {
    await SendToAsync(_eventNames.StationOrdersChanged,
                      new StationOrdersChangedEvent(stationId),
                      [_groupNames.Station(stationId), _groupNames.Admin],
                      ct);
  }

  public async Task PushOrderItemsSettledAsync(OrderItemsSettledEvent payload, CancellationToken ct)
  {
    await SendToAsync(_eventNames.OrderItemsSettled,
                      payload,
                      [_groupNames.Devices, _groupNames.Admin],
                      ct);
  }

  public async Task PushCatalogChangedAsync(CancellationToken ct)
  {
    await SendToAsync(_eventNames.CatalogChanged,
                      new CatalogChangedEvent(),
                      [_groupNames.Devices, _groupNames.Admin],
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
                      [_groupNames.Device(deviceId), _groupNames.Admin],
                      ct);
  }

  private async Task SendToAsync(string eventName,
                                 object payload,
                                 IReadOnlyList<string> groups,
                                 CancellationToken ct)
  {
    ct.ThrowIfCancellationRequested();

    foreach (var group in groups)
    {
      await _hubContext.Clients.Group(group).SendAsync(eventName, payload, ct);
    }
  }
}
