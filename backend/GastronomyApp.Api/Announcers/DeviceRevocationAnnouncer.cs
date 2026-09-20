using GastronomyApp.Api.Hub;

namespace GastronomyApp.Api.Announcers;

public sealed class DeviceRevocationAnnouncer
{
  private readonly DeviceConnectionTerminator _connectionTerminator;
  private readonly HubNotificationDispatcher _dispatcher;

  public DeviceRevocationAnnouncer(HubNotificationDispatcher dispatcher, DeviceConnectionTerminator connectionTerminator)
  {
    _dispatcher = dispatcher;
    _connectionTerminator = connectionTerminator;
  }

  public async Task AnnounceAsync(Guid? revokedDeviceId, CancellationToken cancellationToken)
  {
    if (revokedDeviceId is null)
      return;

    await _dispatcher.PushDeviceRevokedAsync(revokedDeviceId.Value, cancellationToken);
    await _connectionTerminator.TerminateAsync(revokedDeviceId.Value, cancellationToken);
  }
}
