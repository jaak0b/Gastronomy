namespace GastronomyApp.Core.Ports;

public interface IStationsChangeAnnouncer
{
  public Task AnnounceStationsChangedAsync(Guid stationId, CancellationToken cancellationToken);
}
