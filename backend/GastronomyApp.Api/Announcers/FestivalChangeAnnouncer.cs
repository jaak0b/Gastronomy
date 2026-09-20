using GastronomyApp.Api.Hub;

namespace GastronomyApp.Api.Announcers;

public sealed class FestivalChangeAnnouncer
{
  private readonly SavedChangeAnnouncer _announcement;
  private readonly HubNotificationDispatcher _dispatcher;

  public FestivalChangeAnnouncer(HubNotificationDispatcher dispatcher, SavedChangeAnnouncer announcement)
  {
    _dispatcher = dispatcher;
    _announcement = announcement;
  }

  public Task AnnounceAsync()
  {
    return _announcement.TellTheDevicesWithoutFailingTheSavedChangeAsync(_dispatcher.PushFestivalChangedAsync);
  }
}
