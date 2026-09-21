using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Ports;

public interface IStationRepository
{
  public Task<IReadOnlyCollection<Station>> FindAtFestivalAsync(Guid festivalId, CancellationToken cancellationToken);

  public Task<IReadOnlyList<Station>> FindAtFestivalWithOpenItemsAsync(Guid festivalId, CancellationToken cancellationToken);

  public Task<IReadOnlyList<Station>> FindAllAsync(Guid? festivalId, CancellationToken cancellationToken);

  public Task<Station?> FindByIdAsync(Guid stationId, CancellationToken cancellationToken);

  public Task<bool> ExistsAsync(Guid stationId, CancellationToken cancellationToken);

  public Task AddAsync(Station station, CancellationToken cancellationToken);

  public Task SaveChangesAsync(CancellationToken cancellationToken);
}
