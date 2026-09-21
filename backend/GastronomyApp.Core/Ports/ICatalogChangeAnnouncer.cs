namespace GastronomyApp.Core.Ports;

public interface ICatalogChangeAnnouncer
{
  public Task AnnounceCatalogChangedAsync(CancellationToken cancellationToken);
}
