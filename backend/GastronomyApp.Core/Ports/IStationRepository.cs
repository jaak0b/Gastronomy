using GastronomyApp.Core.Entities;
using GastronomyApp.Core.ReadModels;

namespace GastronomyApp.Core.Ports;

public interface IStationRepository
{
  public Task<IReadOnlyCollection<Station>> FindAtFestivalAsync(Guid festivalId, CancellationToken cancellationToken);

  public Task<IReadOnlyList<AdministeredStation>> FindAdministeredAsync(Guid? festivalId, DateTime nowUtc, CancellationToken cancellationToken);

  public Task<Station?> FindByIdAsync(Guid stationId, CancellationToken cancellationToken);

  public Task<bool> ExistsAsync(Guid stationId, CancellationToken cancellationToken);

  public Task AddAsync(Station station, CancellationToken cancellationToken);

  public Task SaveChangesAsync(CancellationToken cancellationToken);
}
