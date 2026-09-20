using GastronomyApp.Api.Hub;

namespace GastronomyApp.Api.Announcers;

public sealed class StationChangeAnnouncer
{
  private readonly SavedChangeAnnouncer _announcement;
  private readonly HubNotificationDispatcher _dispatcher;

  public StationChangeAnnouncer(HubNotificationDispatcher dispatcher, SavedChangeAnnouncer announcement)
  {
    _dispatcher = dispatcher;
    _announcement = announcement;
  }

  public Task AnnounceAsync(Guid stationId)
  {
    return _announcement.TellTheDevicesWithoutFailingTheSavedChangeAsync(cancellationToken => _dispatcher.PushStationsChangedAsync(stationId, cancellationToken));
  }
}
