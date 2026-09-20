using GastronomyApp.Api.Hub;

namespace GastronomyApp.Api.Announcers;

public sealed class StationChangeAnnouncer
{
  private readonly HubNotificationDispatcher _dispatcher;
  private readonly SavedChangeAnnouncer _savedChangeAnnouncer;

  public StationChangeAnnouncer(HubNotificationDispatcher dispatcher, SavedChangeAnnouncer savedChangeAnnouncer)
  {
    _dispatcher = dispatcher;
    _savedChangeAnnouncer = savedChangeAnnouncer;
  }

  public Task AnnounceAsync(Guid stationId)
  {
    return _savedChangeAnnouncer.TellTheDevicesWithoutFailingTheSavedChangeAsync(cancellationToken => _dispatcher.PushStationsChangedAsync(stationId, cancellationToken));
  }
}
