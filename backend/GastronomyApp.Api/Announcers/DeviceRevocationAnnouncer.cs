using GastronomyApp.Api.Hub;
using GastronomyApp.Core.Ports;

namespace GastronomyApp.Api.Announcers;

public sealed class DeviceRevocationAnnouncer : IDeviceRevocationAnnouncer
{
  private readonly DeviceConnectionTerminator _connectionTerminator;
  private readonly HubNotificationDispatcher _dispatcher;
  private readonly SavedChangeAnnouncer _savedChangeAnnouncer;

  public DeviceRevocationAnnouncer(HubNotificationDispatcher dispatcher, DeviceConnectionTerminator connectionTerminator, SavedChangeAnnouncer savedChangeAnnouncer)
  {
    _dispatcher = dispatcher;
    _connectionTerminator = connectionTerminator;
    _savedChangeAnnouncer = savedChangeAnnouncer;
  }

  public async Task AnnounceAsync(Guid revokedDeviceId, CancellationToken cancellationToken)
  {
    await _savedChangeAnnouncer.TellTheDevicesWithoutFailingTheSavedChangeAsync(_ => SignTheDeviceOutAsync(revokedDeviceId, cancellationToken));
  }

  private async Task SignTheDeviceOutAsync(Guid revokedDeviceId, CancellationToken cancellationToken)
  {
    await _dispatcher.PushDeviceRevokedAsync(revokedDeviceId, cancellationToken);
    await _connectionTerminator.TerminateAsync(revokedDeviceId, cancellationToken);
  }
}
