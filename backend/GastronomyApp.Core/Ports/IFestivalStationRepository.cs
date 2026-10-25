using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Ports;

public interface IFestivalStationRepository
{
  public Task<FestivalStation?> FindLinkAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken);
}
