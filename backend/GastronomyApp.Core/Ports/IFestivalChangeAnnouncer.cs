namespace GastronomyApp.Core.Ports;

public interface IFestivalChangeAnnouncer
{
  public Task AnnounceFestivalChangedAsync(CancellationToken cancellationToken);
}
