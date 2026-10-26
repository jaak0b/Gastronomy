using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Ports;

public interface IStationRepository
{
  public Task<IReadOnlyCollection<Guid>> FindStationIdsWithItemsToHandOutAsync(Guid festivalId, CancellationToken cancellationToken);

  public Task<Station?> FindByIdAsync(Guid stationId, CancellationToken cancellationToken);
}
