using GastronomyApp.Api.Hub;

namespace GastronomyApp.Api.Endpoints;

public sealed class FestivalChangeAnnouncer
{
  private readonly SavedChangeAnnouncement _announcement;
  private readonly HubNotificationDispatcher _dispatcher;

  public FestivalChangeAnnouncer(HubNotificationDispatcher dispatcher, SavedChangeAnnouncement announcement)
  {
    _dispatcher = dispatcher;
    _announcement = announcement;
  }

  public Task AnnounceAsync()
  {
    return _announcement.TellTheDevicesWithoutFailingTheSavedChangeAsync(_dispatcher.PushFestivalChangedAsync);
  }
}
