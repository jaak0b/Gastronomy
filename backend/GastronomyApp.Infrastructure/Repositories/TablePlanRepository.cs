using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class TablePlanRepository : ITablePlanRepository
{
  private readonly GastronomyAppDbContext _dbContext;

  public TablePlanRepository(GastronomyAppDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task<IReadOnlyList<Area>> FindTablePlanAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    return await _dbContext.Areas.AsNoTracking()
                           .Where(area => area.FestivalId == festivalId)
                           .OrderBy(area => area.SortOrder)
                           .ThenBy(area => area.Name)
                           .Include(area => area.Tables.OrderBy(table => table.Name))
                           .Include(area => area.StationShapes.OrderBy(shape => shape.FestivalStation.Station.SortOrder).ThenBy(shape => shape.FestivalStation.Station.Name))
                           .ThenInclude(shape => shape.FestivalStation)
                           .ThenInclude(link => link.Station)
                           .ToListAsync(cancellationToken);
  }

  public async Task<Area?> FindAreaAsync(Guid festivalId, Guid areaId, CancellationToken cancellationToken)
  {
    return await _dbContext.Areas.FirstOrDefaultAsync(area => area.Id == areaId && area.FestivalId == festivalId, cancellationToken);
  }

  public async Task<Area?> FindAreaWithContentsAsync(Guid festivalId, Guid areaId, CancellationToken cancellationToken)
  {
    return await _dbContext.Areas.Include(area => area.Tables).Include(area => area.StationShapes).FirstOrDefaultAsync(area => area.Id == areaId && area.FestivalId == festivalId, cancellationToken);
  }

  public async Task<bool> IsAreaNameTakenAsync(Guid festivalId, string name, Guid? areaBeingSaved, CancellationToken cancellationToken)
  {
    IQueryable<Area> namesakes = _dbContext.Areas.AsNoTracking().Where(area => area.FestivalId == festivalId && area.Name == name);

    if (areaBeingSaved is { } savedAreaId)
      namesakes = namesakes.Where(area => area.Id != savedAreaId);

    return await namesakes.AnyAsync(cancellationToken);
  }

  public async Task AddAreaAsync(Area area, CancellationToken cancellationToken)
  {
    await _dbContext.Areas.AddAsync(area, cancellationToken);
  }

  public void RemoveArea(Area area)
  {
    _dbContext.Areas.Remove(area);
  }

  public async Task<bool> HasOrdersInAreaAsync(Guid areaId, CancellationToken cancellationToken)
  {
    return await _dbContext.Orders.AsNoTracking().AnyAsync(order => order.Table.AreaId == areaId, cancellationToken);
  }

  public async Task<bool> HasItemsToHandOutOrSettleInAreaAsync(Guid areaId, CancellationToken cancellationToken)
  {
    return await ItemsToHandOutOrSettle().AnyAsync(item => item.StationOrder.Order.Table.AreaId == areaId, cancellationToken);
  }

  public async Task<Table?> FindTableAsync(Guid festivalId, Guid tableId, CancellationToken cancellationToken)
  {
    return await _dbContext.Tables.FirstOrDefaultAsync(table => table.Id == tableId && table.FestivalId == festivalId, cancellationToken);
  }

  public async Task<bool> IsTableNameTakenAsync(Guid festivalId, string name, Guid? tableBeingSaved, CancellationToken cancellationToken)
  {
    IQueryable<Table> namesakes = _dbContext.Tables.AsNoTracking().Where(table => table.FestivalId == festivalId && table.Name == name);

    if (tableBeingSaved is { } savedTableId)
      namesakes = namesakes.Where(table => table.Id != savedTableId);

    return await namesakes.AnyAsync(cancellationToken);
  }

  public async Task AddTableAsync(Table table, CancellationToken cancellationToken)
  {
    await _dbContext.Tables.AddAsync(table, cancellationToken);
  }

  public void RemoveTable(Table table)
  {
    _dbContext.Tables.Remove(table);
  }

  public void RemoveTables(IReadOnlyCollection<Table> tables)
  {
    ArgumentNullException.ThrowIfNull(tables);

    _dbContext.Tables.RemoveRange(tables);
  }

  public async Task<bool> HasOrdersAtTableAsync(Guid tableId, CancellationToken cancellationToken)
  {
    return await _dbContext.Orders.AsNoTracking().AnyAsync(order => order.TableId == tableId, cancellationToken);
  }

  public async Task<bool> HasItemsToHandOutOrSettleAtTableAsync(Guid tableId, CancellationToken cancellationToken)
  {
    return await ItemsToHandOutOrSettle().AnyAsync(item => item.StationOrder.Order.TableId == tableId, cancellationToken);
  }

  public async Task<FestivalStationShape?> FindStationShapeAsync(Guid festivalStationId, CancellationToken cancellationToken)
  {
    return await _dbContext.FestivalStationShapes.FirstOrDefaultAsync(shape => shape.FestivalStationId == festivalStationId, cancellationToken);
  }

  public async Task AddStationShapeAsync(FestivalStationShape shape, CancellationToken cancellationToken)
  {
    await _dbContext.FestivalStationShapes.AddAsync(shape, cancellationToken);
  }

  public void RemoveStationShape(FestivalStationShape shape)
  {
    _dbContext.FestivalStationShapes.Remove(shape);
  }

  public void RemoveStationShapes(IReadOnlyCollection<FestivalStationShape> shapes)
  {
    ArgumentNullException.ThrowIfNull(shapes);

    _dbContext.FestivalStationShapes.RemoveRange(shapes);
  }

  public async Task SaveChangesAsync(CancellationToken cancellationToken)
  {
    await _dbContext.SaveChangesAsync(cancellationToken);
  }

  private IQueryable<OrderItem> ItemsToHandOutOrSettle()
  {
    return _dbContext.OrderItems.AsNoTracking().Where(item => item.FulfilledAtUtc == null || item.SettledAtUtc == null);
  }
}
