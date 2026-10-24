using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Ports;

public interface ITablePlanRepository
{
  public Task<IReadOnlyList<Area>> FindTablePlanAsync(Guid festivalId, CancellationToken cancellationToken);

  public Task<Area?> FindAreaAsync(Guid festivalId, Guid areaId, CancellationToken cancellationToken);

  public Task<Area?> FindAreaWithContentsAsync(Guid festivalId, Guid areaId, CancellationToken cancellationToken);

  public Task<bool> IsAreaNameTakenAsync(Guid festivalId, string name, Guid? areaBeingSaved, CancellationToken cancellationToken);

  public Task AddAreaAsync(Area area, CancellationToken cancellationToken);

  public void RemoveArea(Area area);

  public Task<Table?> FindTableAsync(Guid festivalId, Guid tableId, CancellationToken cancellationToken);

  public Task<bool> IsTableNameTakenAsync(Guid festivalId, string name, Guid? tableBeingSaved, CancellationToken cancellationToken);

  public Task AddTableAsync(Table table, CancellationToken cancellationToken);

  public void RemoveTable(Table table);

  public void RemoveTables(IReadOnlyCollection<Table> tables);

  public Task<FestivalStationShape?> FindStationShapeAsync(Guid festivalStationId, CancellationToken cancellationToken);

  public Task AddStationShapeAsync(FestivalStationShape shape, CancellationToken cancellationToken);

  public void RemoveStationShape(FestivalStationShape shape);

  public void RemoveStationShapes(IReadOnlyCollection<FestivalStationShape> shapes);

  public Task SaveChangesAsync(CancellationToken cancellationToken);
}
