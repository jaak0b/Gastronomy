using GastronomyApp.Api.Hub;
using GastronomyApp.Core.Ports;
using GastronomyApp.Infrastructure;
using GastronomyApp.Infrastructure.Ports;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Endpoints;

public sealed class DeviceRevoker
{
  private readonly DeviceConnectionTerminator _connectionTerminator;
  private readonly IDeviceTokenStore _deviceTokenStore;
  private readonly HubNotificationDispatcher _dispatcher;

  public DeviceRevoker(IDeviceTokenStore deviceTokenStore,
                       HubNotificationDispatcher dispatcher,
                       DeviceConnectionTerminator connectionTerminator)
  {
    _deviceTokenStore = deviceTokenStore;
    _dispatcher = dispatcher;
    _connectionTerminator = connectionTerminator;
  }

  public async Task RevokeAsync(Guid deviceId, CancellationToken cancellationToken)
  {
    await _deviceTokenStore.RevokeAsync(deviceId, cancellationToken);
    await _dispatcher.PushDeviceRevokedAsync(deviceId, cancellationToken);
    await _connectionTerminator.TerminateAsync(deviceId, cancellationToken);
  }
}

public sealed class OutstandingInvitationLookup
{
  private readonly IClock _clock;
  private readonly GastronomyAppDbContext _dbContext;

  public OutstandingInvitationLookup(GastronomyAppDbContext dbContext, IClock clock)
  {
    _dbContext = dbContext;
    _clock = clock;
  }

  public async Task<HashSet<Guid>> InvitationIdsStillOutstandingAsync(CancellationToken cancellationToken)
  {
    var now = _clock.UtcNow;

    List<Guid> outstanding = await _dbContext.EnrolmentInvitations
                                             .AsNoTracking()
                                             .Where(invitation => invitation.ConsumedAtUtc == null
                                                                  && invitation.ExpiresAtUtc > now)
                                             .Select(invitation => invitation.Id)
                                             .ToListAsync(cancellationToken);

    return [.. outstanding];
  }

  public async Task<Dictionary<Guid, DateTime>> LastSeenByDeviceIdAsync(CancellationToken cancellationToken)
  {
    return await _dbContext.Devices
                           .AsNoTracking()
                           .ToDictionaryAsync(device => device.Id, device => device.LastSeenAtUtc, cancellationToken);
  }
}
