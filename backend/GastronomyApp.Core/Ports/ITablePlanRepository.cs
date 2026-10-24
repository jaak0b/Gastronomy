using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Ports;

public interface ITablePlanRepository
{
  public Task<IReadOnlyList<ListedArea>> FindTablePlanAsync(Guid festivalId, CancellationToken cancellationToken);

  public Task<IReadOnlyList<ShownArea>> FindShownAreasWithItemFlagsAsync(Guid festivalId, CancellationToken cancellationToken);

  public Task<Area?> FindAreaAsync(Guid festivalId, Guid areaId, CancellationToken cancellationToken);

  public Task<IReadOnlyList<Area>> FindAreasInOrderAsync(Guid festivalId, CancellationToken cancellationToken);

  public Task<bool> IsAreaNameTakenAsync(Guid festivalId, string name, Guid? areaBeingSaved, CancellationToken cancellationToken);

  public Task AddAreaAsync(Area area, CancellationToken cancellationToken);

  public Task<bool> HasItemsToHandOutOrSettleInAreaAsync(Guid areaId, CancellationToken cancellationToken);

  public Task<Table?> FindTableAsync(Guid festivalId, Guid tableId, CancellationToken cancellationToken);

  public Task<bool> IsTableNameTakenAsync(Guid festivalId, string name, Guid? tableBeingSaved, CancellationToken cancellationToken);

  public Task AddTableAsync(Table table, CancellationToken cancellationToken);

  public Task<bool> HasItemsToHandOutOrSettleAtTableAsync(Guid tableId, CancellationToken cancellationToken);

  public Task<FestivalStationShape?> FindStationShapeAsync(Guid festivalStationId, CancellationToken cancellationToken);

  public Task AddStationShapeAsync(FestivalStationShape shape, CancellationToken cancellationToken);

  public void RemoveStationShape(FestivalStationShape shape);

  public Task SaveChangesAsync(CancellationToken cancellationToken);
}
