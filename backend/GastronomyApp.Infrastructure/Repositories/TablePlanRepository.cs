using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;
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

  public async Task<IReadOnlyList<ListedArea>> FindTablePlanAsync(Guid festivalId, bool countsItemsToHandOutOrSettle, CancellationToken cancellationToken)
  {
    IQueryable<OrderItem> itemsToHandOutOrSettle = _dbContext.OrderItems.AsNoTracking().WhereNotHandedOutOrNotSettled();

    return await _dbContext.Areas.AsNoTracking()
                           .Where(area => area.FestivalId == festivalId)
                           .OrderBy(area => area.SortOrder)
                           .ThenBy(area => area.Name)
                           .Select(area => new ListedArea(area,
                                                          countsItemsToHandOutOrSettle && itemsToHandOutOrSettle.Any(item => item.StationOrder.Order.Table.AreaId == area.Id),
                                                          area.Tables.OrderBy(table => table.Name).Select(table => new ListedTable(table, countsItemsToHandOutOrSettle && itemsToHandOutOrSettle.Any(item => item.StationOrder.Order.TableId == table.Id))).ToList(),
                                                          area.StationShapes.OrderBy(shape => shape.FestivalStation.Station.SortOrder)
                                                              .ThenBy(shape => shape.FestivalStation.Station.Name)
                                                              .Select(shape => new NamedStationShape(shape, shape.FestivalStation.StationId, shape.FestivalStation.Station.Name))
                                                              .ToList()))
                           .ToListAsync(cancellationToken);
  }

  public async Task<IReadOnlyList<ShownArea>> FindShownAreasWithItemFlagsAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    return await _dbContext.Areas.AsNoTracking()
                           .Where(area => area.FestivalId == festivalId && !area.IsHidden)
                           .OrderBy(area => area.SortOrder)
                           .ThenBy(area => area.Name)
                           .Select(area => new ShownArea(area,
                                                         area.Tables.Where(table => !table.IsHidden)
                                                             .OrderBy(table => table.Name)
                                                             .Select(table => new ShownTable(table,
                                                                                             table.Orders.Select(order => order.StationOrders.SelectMany(stationOrder => stationOrder.Items).Select(item => new OrderItemFlags(item.FulfilledAtUtc != null, item.SettledAtUtc != null)).ToList()).ToList()))
                                                             .ToList(),
                                                         area.StationShapes.OrderBy(shape => shape.FestivalStation.Station.SortOrder)
                                                             .ThenBy(shape => shape.FestivalStation.Station.Name)
                                                             .Select(shape => new NamedStationShape(shape, shape.FestivalStation.StationId, shape.FestivalStation.Station.Name))
                                                             .ToList()))
                           .ToListAsync(cancellationToken);
  }

  public async Task<Area?> FindAreaAsync(Guid festivalId, Guid areaId, CancellationToken cancellationToken)
  {
    return await _dbContext.Areas.FirstOrDefaultAsync(area => area.Id == areaId && area.FestivalId == festivalId, cancellationToken);
  }

  public async Task<IReadOnlyList<Area>> FindAreasInOrderAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    return await _dbContext.Areas.Where(area => area.FestivalId == festivalId).OrderBy(area => area.SortOrder).ThenBy(area => area.Name).ToListAsync(cancellationToken);
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

  public async Task<bool> HasItemsToHandOutOrSettleInAreaAsync(Guid areaId, CancellationToken cancellationToken)
  {
    return await _dbContext.OrderItems.AsNoTracking().WhereNotHandedOutOrNotSettled().AnyAsync(item => item.StationOrder.Order.Table.AreaId == areaId, cancellationToken);
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

  public async Task<bool> HasItemsToHandOutOrSettleAtTableAsync(Guid tableId, CancellationToken cancellationToken)
  {
    return await _dbContext.OrderItems.AsNoTracking().WhereNotHandedOutOrNotSettled().AnyAsync(item => item.StationOrder.Order.TableId == tableId, cancellationToken);
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

  public async Task SaveChangesAsync(CancellationToken cancellationToken)
  {
    await _dbContext.SaveChangesAsync(cancellationToken);
  }
}
