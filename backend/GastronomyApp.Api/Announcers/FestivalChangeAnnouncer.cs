using GastronomyApp.Api.Hub;

namespace GastronomyApp.Api.Announcers;

public sealed class FestivalChangeAnnouncer
{
  private readonly HubNotificationDispatcher _dispatcher;
  private readonly SavedChangeAnnouncer _savedChangeAnnouncer;

  public FestivalChangeAnnouncer(HubNotificationDispatcher dispatcher, SavedChangeAnnouncer savedChangeAnnouncer)
  {
    _dispatcher = dispatcher;
    _savedChangeAnnouncer = savedChangeAnnouncer;
  }

  public Task AnnounceAsync()
  {
    return _savedChangeAnnouncer.TellTheDevicesWithoutFailingTheSavedChangeAsync(_dispatcher.PushFestivalChangedAsync);
  }
}
