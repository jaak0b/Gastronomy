using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Infrastructure.Persistence;
using GastronomyApp.Infrastructure.QueryRows;
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

  public async Task<IReadOnlyList<TableOrderRecord>> FindTableOrdersAsync(Guid festivalId, string tableName, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(tableName);

    List<TableOrderHeaderRow> headers = await _dbContext.Orders.AsNoTracking()
                                                        .Where(order => order.FestivalId == festivalId && order.TableName == tableName)
                                                        .Join(_dbContext.StaffMembers.AsNoTracking(),
                                                              order => order.StaffMemberId,
                                                              staffMember => staffMember.Id,
                                                              (order, staffMember) => new TableOrderHeaderRow
                                                                                      {
                                                                                        OrderId = order.Id,
                                                                                        GlobalOrderNumber = order.GlobalOrderNumber,
                                                                                        CreatedAtUtc = order.CreatedAtUtc,
                                                                                        StaffMemberName = staffMember.Name
                                                                                      })
                                                        .OrderByDescending(header => header.CreatedAtUtc)
                                                        .ThenByDescending(header => header.GlobalOrderNumber)
                                                        .ToListAsync(cancellationToken);

    if (headers.Count == 0)
      return [];

    List<Guid> orderIds = headers.Select(header => header.OrderId).ToList();

    List<TableOrderItemRow> items = await _dbContext.OrderItems.AsNoTracking()
                                                    .Join(_dbContext.StationOrders.AsNoTracking(),
                                                          item => item.StationOrderId,
                                                          stationOrder => stationOrder.Id,
                                                          (item, stationOrder) => new TableOrderItemRow
                                                                                  {
                                                                                    OrderId = stationOrder.OrderId,
                                                                                    Item = item
                                                                                  })
                                                    .Where(row => orderIds.Contains(row.OrderId))
                                                    .OrderBy(row => row.Item.ItemName)
                                                    .ThenBy(row => row.Item.Note)
                                                    .ThenBy(row => row.Item.Id)
                                                    .ToListAsync(cancellationToken);

    ILookup<Guid, TableOrderItemRow> itemsByOrder = items.ToLookup(row => row.OrderId);

    return headers.Select(header => new TableOrderRecord
                                    {
                                      OrderId = header.OrderId,
                                      GlobalOrderNumber = header.GlobalOrderNumber,
                                      CreatedAtUtc = header.CreatedAtUtc,
                                      StaffMemberName = header.StaffMemberName,
                                      Items = itemsByOrder[header.OrderId]
                                       .Select(row => new TableOrderRecordItem
                                                      {
                                                        OrderItemId = row.Item.Id,
                                                        OrderId = header.OrderId,
                                                        GlobalOrderNumber = header.GlobalOrderNumber,
                                                        ItemName = row.Item.ItemName,
                                                        Note = row.Item.Note,
                                                        UnitPriceCents = row.Item.UnitPriceCents,
                                                        OrderedAtUtc = header.CreatedAtUtc,
                                                        FulfilledAtUtc = row.Item.FulfilledAtUtc,
                                                        SettledAtUtc = row.Item.SettledAtUtc
                                                      })
                                       .ToList()
                                    })
                  .ToList();
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
