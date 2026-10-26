using GastronomyApp.Core.Ports;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Core.Services;

public sealed class DeviceOwnerRetirement
{
  private readonly IAfterCommitActions _afterCommitActions;
  private readonly IDeviceRevocationAnnouncer _announcer;
  private readonly IGastronomyDatabase _database;
  private readonly DeviceOwnerLookup _lookup;
  private readonly TimeProvider _timeProvider;

  public DeviceOwnerRetirement(IGastronomyDatabase database, DeviceOwnerLookup lookup, IDeviceRevocationAnnouncer announcer, IAfterCommitActions afterCommitActions, TimeProvider timeProvider)
  {
    _database = database;
    _lookup = lookup;
    _announcer = announcer;
    _afterCommitActions = afterCommitActions;
    _timeProvider = timeProvider;
  }

  public async Task WithdrawOutstandingInvitationAsync(Guid? invitationId, CancellationToken cancellationToken)
  {
    if (invitationId is null)
      return;

    var invitation = await _database.EnrolmentInvitations.FirstOrDefaultAsync(candidate => candidate.Id == invitationId.Value, cancellationToken);

    if (invitation is not null && invitation.ConsumedAtUtc is null)
      invitation.ConsumedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
  }

  public async Task<Guid?> RevokeDeviceAsync(Guid? deviceId, CancellationToken cancellationToken)
  {
    if (deviceId is null)
      return null;

    await RemoveDeviceAsync(deviceId.Value, cancellationToken);
    await _afterCommitActions.RunWhenCommittedAsync(announcementCancellationToken => _announcer.AnnounceDeviceRevokedAsync(deviceId.Value, announcementCancellationToken), cancellationToken);

    return deviceId;
  }

  private async Task RemoveDeviceAsync(Guid deviceId, CancellationToken cancellationToken)
  {
    var device = await _database.Devices.FirstOrDefaultAsync(candidate => candidate.Id == deviceId, cancellationToken);

    if (device is null)
      return;

    var owner = await _lookup.FindByDeviceAsync(deviceId, cancellationToken);

    if (owner is not null)
    {
      owner.DeviceId = null;
      owner.Device = null;
    }

    _database.Devices.Remove(device);
    await _database.SaveChangesAsync(cancellationToken);
  }
}
