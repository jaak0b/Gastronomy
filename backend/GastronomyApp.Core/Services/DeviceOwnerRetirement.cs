using GastronomyApp.Core.Ports;

namespace GastronomyApp.Core.Services;

public sealed class DeviceOwnerRetirement
{
  private readonly IClock _clock;
  private readonly IDeviceTokenStore _deviceTokenStore;
  private readonly IEnrolmentInvitationStore _invitationStore;

  public DeviceOwnerRetirement(IEnrolmentInvitationStore invitationStore,
                               IDeviceTokenStore deviceTokenStore,
                               IClock clock)
  {
    _invitationStore = invitationStore;
    _deviceTokenStore = deviceTokenStore;
    _clock = clock;
  }

  public async Task WithdrawOutstandingInvitationAsync(Guid? invitationId, CancellationToken cancellationToken)
  {
    if (invitationId is null)
    {
      return;
    }

    await _invitationStore.ConsumeAsync(invitationId.Value, _clock.UtcNow, cancellationToken);
  }

  public async Task<Guid?> RevokeDeviceAsync(Guid? deviceId, CancellationToken cancellationToken)
  {
    if (deviceId is null)
    {
      return null;
    }

    await _deviceTokenStore.RevokeAsync(deviceId.Value, cancellationToken);

    return deviceId;
  }
}
