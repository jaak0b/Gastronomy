using GastronomyApp.Api.Hub;

namespace GastronomyApp.Api.Announcers;

public sealed class CatalogChangeAnnouncer
{
  private readonly HubNotificationDispatcher _dispatcher;

  public CatalogChangeAnnouncer(HubNotificationDispatcher dispatcher)
  {
    _dispatcher = dispatcher;
  }

  public async Task AnnounceAsync(CancellationToken cancellationToken)
  {
    await _dispatcher.PushCatalogChangedAsync(cancellationToken);
  }
}
