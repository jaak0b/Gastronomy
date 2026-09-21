namespace GastronomyApp.Core.Ports;

public interface IStationOrdersAnnouncer
{
  public Task AnnounceStationOrdersChangedAsync(Guid stationId, CancellationToken cancellationToken);
}
