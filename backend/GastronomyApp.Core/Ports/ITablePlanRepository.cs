using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Ports;

public interface ITablePlanRepository
{
  public Task<Area?> FindAreaAsync(Guid festivalId, Guid areaId, CancellationToken cancellationToken);

  public Task<Table?> FindTableAsync(Guid festivalId, Guid tableId, CancellationToken cancellationToken);

  public Task<FestivalStationShape?> FindStationShapeAsync(Guid festivalStationId, CancellationToken cancellationToken);

  public Task AddStationShapeAsync(FestivalStationShape shape, CancellationToken cancellationToken);

  public void RemoveStationShape(FestivalStationShape shape);

  public Task SaveChangesAsync(CancellationToken cancellationToken);
}
