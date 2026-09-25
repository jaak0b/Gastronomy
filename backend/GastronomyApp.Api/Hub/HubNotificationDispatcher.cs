using GastronomyApp.Contracts.Events;
using GastronomyApp.Core.Announcements;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using MapsterMapper;
using Microsoft.AspNetCore.SignalR;

namespace GastronomyApp.Api.Hub;

public sealed class HubNotificationDispatcher : ICommittedChangeAnnouncer, IEnrolmentCompletionAnnouncer, IDeviceRevocationAnnouncer
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

  public Task AnnounceAsync(HubEvent hubEvent, CancellationToken cancellationToken)
  {
    return hubEvent switch
    {
      HubEvent.ConfigurationChanged => SendGuardedAsync(Names.HubEvents.ConfigurationChanged,
                                                        [],
                                                        [
                                                          Names.HubGroups.Devices,
                                                          Names.HubGroups.Stations,
                                                          Names.HubGroups.Admin
                                                        ],
                                                        cancellationToken),
      HubEvent.OrdersChanged => SendGuardedAsync(Names.HubEvents.OrdersChanged,
                                                 [],
                                                 [
                                                   Names.HubGroups.Devices,
                                                   Names.HubGroups.Admin,
                                                   Names.HubGroups.Stations
                                                 ],
                                                 cancellationToken),
      _ => throw new ArgumentOutOfRangeException(nameof(hubEvent), hubEvent, "Only a hub event that screens listen for can be announced.")
    };
  }

  public Task AnnounceEnrolmentCompletedAsync(IDeviceOwner owner, CancellationToken cancellationToken)
  {
    return SendGuardedAsync(Names.HubEvents.EnrolmentCompleted, [_mapper.Map<IDeviceOwner, EnrolmentCompletedEvent>(owner)], [Names.HubGroups.Admin], cancellationToken);
  }

  public Task AnnounceDeviceRevokedAsync(Guid revokedDeviceId, CancellationToken cancellationToken)
  {
    return _guard.TellTheDevicesWithoutFailingTheSavedChangeAsync(async announcementCancellationToken =>
                                                                  {
                                                                    await SendToGroupsAsync(Names.HubEvents.DeviceRevoked,
                                                                                            [new DeviceRevokedEvent(revokedDeviceId)],
                                                                                            [
                                                                                              Names.HubGroups.BuildDeviceGroupName(revokedDeviceId),
                                                                                              Names.HubGroups.Admin
                                                                                            ],
                                                                                            announcementCancellationToken);
                                                                    await _connectionTerminator.TerminateAsync(revokedDeviceId, announcementCancellationToken);
                                                                  },
                                                                  cancellationToken);
  }

  private Task SendGuardedAsync(string eventName, object[] arguments, IReadOnlyList<string> groups, CancellationToken cancellationToken)
  {
    return _guard.TellTheDevicesWithoutFailingTheSavedChangeAsync(announcementCancellationToken => SendToGroupsAsync(eventName, arguments, groups, announcementCancellationToken), cancellationToken);
  }

  private async Task SendToGroupsAsync(string eventName, object[] arguments, IReadOnlyList<string> groups, CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    foreach (var group in groups)
      await _hubContext.Clients.Group(group).SendCoreAsync(eventName, arguments, cancellationToken);
  }
}
