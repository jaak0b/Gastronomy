using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class OpenItemRepository : IOpenItemRepository
{
  private readonly GastronomyAppDbContext _dbContext;

  public OpenItemRepository(GastronomyAppDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task<IReadOnlyList<OrderItem>> FindOpenAtFestivalAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    List<OrderItem> openItems = await ItemsAtFestival(festivalId).AsNoTracking().Where(item => item.SettledAtUtc == null).ToListAsync(cancellationToken);

    await AttachStationOrdersWithTheirOrdersAsync(openItems, cancellationToken);

    return openItems;
  }

  public async Task<IReadOnlyList<OrderItem>> FindForSettlementAsync(IReadOnlyCollection<Guid> orderItemIds, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(orderItemIds);

    List<Guid> ids = orderItemIds.ToList();

    List<OrderItem> selectedItems = await _dbContext.OrderItems.Where(item => ids.Contains(item.Id)).ToListAsync(cancellationToken);

    await AttachStationOrdersWithTheirOrdersAsync(selectedItems, cancellationToken);

    return selectedItems;
  }

  public async Task<Table?> FindTableWithOrdersAsync(Guid festivalId, Guid tableId, CancellationToken cancellationToken)
  {
    return await _dbContext.Tables.AsNoTracking()
                           .Where(table => table.Id == tableId && table.FestivalId == festivalId)
                           .Include(table => table.Orders.OrderByDescending(order => order.CreatedAtUtc).ThenByDescending(order => order.GlobalOrderNumber))
                           .ThenInclude(order => order.StaffMember)
                           .Include(table => table.Orders)
                           .ThenInclude(order => order.StationOrders)
                           .ThenInclude(stationOrder => stationOrder.Items)
                           .FirstOrDefaultAsync(cancellationToken);
  }

  public async Task SaveChangesAsync(CancellationToken cancellationToken)
  {
    await _dbContext.SaveChangesAsync(cancellationToken);
  }

  private async Task AttachStationOrdersWithTheirOrdersAsync(IReadOnlyCollection<OrderItem> items, CancellationToken cancellationToken)
  {
    List<Guid> stationOrderIds = items.Select(item => item.StationOrderId).Distinct().ToList();

    Dictionary<Guid, StationOrder> stationOrdersById = await _dbContext.StationOrders.Where(stationOrder => stationOrderIds.Contains(stationOrder.Id)).Include(stationOrder => stationOrder.Order).ThenInclude(order => order.Table).ToDictionaryAsync(stationOrder => stationOrder.Id, cancellationToken);

    foreach (var item in items)
      if (stationOrdersById.TryGetValue(item.StationOrderId, out var stationOrder))
        item.StationOrder = stationOrder;
  }

  private IQueryable<OrderItem> ItemsAtFestival(Guid festivalId)
  {
    IQueryable<Guid> stationOrderIdsOfAnotherFestival = _dbContext.StationOrders.AsNoTracking()
                                                                  .Join(_dbContext.Orders.AsNoTracking(),
                                                                        stationOrder => stationOrder.OrderId,
                                                                        order => order.Id,
                                                                        (stationOrder, order) => new
                                                                                                 {
                                                                                                   StationOrder = stationOrder,
                                                                                                   Order = order
                                                                                                 })
                                                                  .Where(joined => joined.Order.FestivalId != festivalId)
                                                                  .Select(joined => joined.StationOrder.Id);

    return _dbContext.OrderItems.Where(item => !stationOrderIdsOfAnotherFestival.Contains(item.StationOrderId));
  }
}
