using GastronomyApp.Contracts.Events;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using MapsterMapper;
using Microsoft.AspNetCore.SignalR;

namespace GastronomyApp.Api.Hub;

public sealed class HubNotificationDispatcher : IStationOrdersAnnouncer, IOrderStatusAnnouncer, ICatalogChangeAnnouncer, IFestivalChangeAnnouncer, IStationsChangeAnnouncer, ISettlementAnnouncer, IEnrolmentCompletionAnnouncer, IDeviceRevocationAnnouncer
{
  private readonly DeviceConnectionTerminator _connectionTerminator;
  private readonly IAnnouncementGuard _guard;
  private readonly IHubContext<GastronomyHub> _hubContext;
  private readonly IMapper _mapper;

  public HubNotificationDispatcher(IHubContext<GastronomyHub> hubContext, IAnnouncementGuard guard, DeviceConnectionTerminator connectionTerminator, IMapper mapper)
  {
    _hubContext = hubContext;
    _guard = guard;
    _connectionTerminator = connectionTerminator;
    _mapper = mapper;
  }

  public Task AnnounceStationOrdersChangedAsync(Guid stationId, CancellationToken cancellationToken)
  {
    return AnnounceAsync(Names.HubEvents.StationOrdersChanged,
                         new StationOrdersChangedEvent(stationId),
                         [
                           Names.HubGroups.Devices,
                           Names.HubGroups.BuildStationGroupName(stationId),
                           Names.HubGroups.Admin
                         ],
                         cancellationToken);
  }

  public Task AnnounceOrderStatusChangedAsync(Order order, CancellationToken cancellationToken)
  {
    return AnnounceAsync(Names.HubEvents.OrderStatusChanged,
                         _mapper.Map<OrderStatusChangedEvent>(order),
                         [
                           Names.HubGroups.Devices,
                           Names.HubGroups.Admin
                         ],
                         cancellationToken);
  }

  public Task AnnounceCatalogChangedAsync(CancellationToken cancellationToken)
  {
    return AnnounceAsync(Names.HubEvents.CatalogChanged,
                         new CatalogChangedEvent(),
                         [
                           Names.HubGroups.Devices,
                           Names.HubGroups.Admin
                         ],
                         cancellationToken);
  }

  public Task AnnounceFestivalChangedAsync(CancellationToken cancellationToken)
  {
    return AnnounceAsync(Names.HubEvents.FestivalChanged,
                         new FestivalChangedEvent(),
                         [
                           Names.HubGroups.Devices,
                           Names.HubGroups.Stations,
                           Names.HubGroups.Admin
                         ],
                         cancellationToken);
  }

  public Task AnnounceStationsChangedAsync(Guid stationId, CancellationToken cancellationToken)
  {
    return AnnounceAsync(Names.HubEvents.StationsChanged,
                         new StationsChangedEvent(),
                         [
                           Names.HubGroups.Devices,
                           Names.HubGroups.Admin,
                           Names.HubGroups.BuildStationGroupName(stationId)
                         ],
                         cancellationToken);
  }

  public Task<bool> AnnounceOrderItemsSettledAsync(IReadOnlyList<Guid> settledOrderItemIds, IReadOnlyList<string> tableNames, CancellationToken cancellationToken)
  {
    return AnnounceAsync(Names.HubEvents.OrderItemsSettled,
                         new OrderItemsSettledEvent(settledOrderItemIds, tableNames),
                         [
                           Names.HubGroups.Devices,
                           Names.HubGroups.Admin
                         ],
                         cancellationToken);
  }

  public Task AnnounceEnrolmentCompletedAsync(IDeviceOwner owner, CancellationToken cancellationToken)
  {
    return AnnounceAsync(Names.HubEvents.EnrolmentCompleted, _mapper.Map<IDeviceOwner, EnrolmentCompletedEvent>(owner), [Names.HubGroups.Admin], cancellationToken);
  }

  public Task AnnounceDeviceRevokedAsync(Guid revokedDeviceId, CancellationToken cancellationToken)
  {
    return _guard.TellTheDevicesWithoutFailingTheSavedChangeAsync(async announcementCancellationToken =>
                                                                  {
                                                                    await SendToGroupsAsync(Names.HubEvents.DeviceRevoked,
                                                                                            new DeviceRevokedEvent(revokedDeviceId),
                                                                                            [
                                                                                              Names.HubGroups.BuildDeviceGroupName(revokedDeviceId),
                                                                                              Names.HubGroups.Admin
                                                                                            ],
                                                                                            announcementCancellationToken);
                                                                    await _connectionTerminator.TerminateAsync(revokedDeviceId, announcementCancellationToken);
                                                                  },
                                                                  cancellationToken);
  }

  private Task<bool> AnnounceAsync(string eventName, object payload, IReadOnlyList<string> groups, CancellationToken cancellationToken)
  {
    return _guard.TellTheDevicesWithoutFailingTheSavedChangeAsync(announcementCancellationToken => SendToGroupsAsync(eventName, payload, groups, announcementCancellationToken), cancellationToken);
  }

  private async Task SendToGroupsAsync(string eventName, object payload, IReadOnlyList<string> groups, CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    foreach (var group in groups)
      await _hubContext.Clients.Group(group).SendAsync(eventName, payload, cancellationToken);
  }
}
