using GastronomyApp.Api.Hub;

namespace GastronomyApp.Api.Endpoints;

public sealed class StationChangeAnnouncer
{
  private readonly SavedChangeAnnouncement _announcement;
  private readonly HubNotificationDispatcher _dispatcher;

  public StationChangeAnnouncer(HubNotificationDispatcher dispatcher, SavedChangeAnnouncement announcement)
  {
    _dispatcher = dispatcher;
    _announcement = announcement;
  }

  public Task AnnounceAsync(Guid stationId)
  {
    return _announcement.TellTheDevicesWithoutFailingTheSavedChangeAsync(cancellationToken => _dispatcher.PushStationsChangedAsync(stationId, cancellationToken));
  }
}
