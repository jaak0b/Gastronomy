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

  public async Task<IReadOnlyList<string>> FindTableNamesAtFestivalAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    List<string> usedNames = await _dbContext.Orders.AsNoTracking().Where(order => order.FestivalId == festivalId).Select(order => order.TableName).ToListAsync(cancellationToken);

    return usedNames.Distinct(StringComparer.Ordinal).OrderBy(tableName => tableName, StringComparer.Ordinal).ToList();
  }

  public async Task<IReadOnlyList<Order>> FindTableOrdersAsync(Guid festivalId, string tableName, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(tableName);

    return await _dbContext.Orders.AsNoTracking()
                           .Where(order => order.FestivalId == festivalId && order.TableName == tableName)
                           .Include(order => order.StaffMember)
                           .Include(order => order.StationOrders)
                           .ThenInclude(stationOrder => stationOrder.Items)
                           .OrderByDescending(order => order.CreatedAtUtc)
                           .ThenByDescending(order => order.GlobalOrderNumber)
                           .ToListAsync(cancellationToken);
  }

  public async Task SaveChangesAsync(CancellationToken cancellationToken)
  {
    await _dbContext.SaveChangesAsync(cancellationToken);
  }

  private async Task AttachStationOrdersWithTheirOrdersAsync(IReadOnlyCollection<OrderItem> items, CancellationToken cancellationToken)
  {
    List<Guid> stationOrderIds = items.Select(item => item.StationOrderId).Distinct().ToList();

    Dictionary<Guid, StationOrder> stationOrdersById = await _dbContext.StationOrders.Where(stationOrder => stationOrderIds.Contains(stationOrder.Id)).Include(stationOrder => stationOrder.Order).ToDictionaryAsync(stationOrder => stationOrder.Id, cancellationToken);

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
