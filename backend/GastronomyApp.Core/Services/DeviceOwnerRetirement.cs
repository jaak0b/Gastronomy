using GastronomyApp.Core.Ports;

namespace GastronomyApp.Core.Services;

public sealed class DeviceOwnerRetirement
{
  private readonly IAfterCommitActions _afterCommitActions;
  private readonly IDeviceRevocationAnnouncer _announcer;
  private readonly TimeProvider _timeProvider;
  private readonly IDeviceTokenStore _deviceTokenStore;
  private readonly IEnrolmentInvitationStore _invitationStore;

  public DeviceOwnerRetirement(IEnrolmentInvitationStore invitationStore, IDeviceTokenStore deviceTokenStore, IDeviceRevocationAnnouncer announcer, IAfterCommitActions afterCommitActions, TimeProvider timeProvider)
  {
    _invitationStore = invitationStore;
    _deviceTokenStore = deviceTokenStore;
    _announcer = announcer;
    _afterCommitActions = afterCommitActions;
    _timeProvider = timeProvider;
  }

  public async Task WithdrawOutstandingInvitationAsync(Guid? invitationId, CancellationToken cancellationToken)
  {
    if (invitationId is null)
      return;

    await _invitationStore.ConsumeAsync(invitationId.Value, _timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
  }

  public async Task<Guid?> RevokeDeviceAsync(Guid? deviceId, CancellationToken cancellationToken)
  {
    if (deviceId is null)
      return null;

    await _deviceTokenStore.RevokeAsync(deviceId.Value, cancellationToken);
    await _afterCommitActions.RunWhenCommittedAsync(announcementCancellationToken => _announcer.AnnounceAsync(deviceId.Value, announcementCancellationToken), cancellationToken);

    return deviceId;
  }
}
