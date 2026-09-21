using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Infrastructure.Persistence;
using GastronomyApp.Infrastructure.QueryRows;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class OpenItemRepository : IOpenItemRepository
{
  private readonly GastronomyAppDbContext _dbContext;
  private readonly TypeAdapterConfig _mapperConfig;

  public OpenItemRepository(GastronomyAppDbContext dbContext, TypeAdapterConfig mapperConfig)
  {
    _dbContext = dbContext;
    _mapperConfig = mapperConfig;
  }

  public async Task<IReadOnlyList<OrderItem>> FindOpenAtFestivalAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    return await ItemsAtFestival(festivalId).Where(item => item.SettledAtUtc == null).ToListAsync(cancellationToken);
  }

  public async Task<IReadOnlyList<OrderItem>> FindForSettlementAsync(IReadOnlyCollection<Guid> orderItemIds, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(orderItemIds);

    List<Guid> ids = orderItemIds.ToList();

    return await _dbContext.OrderItems.Where(item => ids.Contains(item.Id)).ToListAsync(cancellationToken);
  }

  public async Task<IReadOnlyDictionary<Guid, OrderItemOwner>> FindOwnersAsync(IReadOnlyCollection<Guid> orderItemIds, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(orderItemIds);

    List<Guid> ids = orderItemIds.ToList();

    List<OrderItemOwnerRow> rows = await _dbContext.OrderItems.AsNoTracking()
                                                   .Where(item => ids.Contains(item.Id))
                                                   .Join(_dbContext.StationOrders.AsNoTracking(),
                                                         item => item.StationOrderId,
                                                         stationOrder => stationOrder.Id,
                                                         (item, stationOrder) => new
                                                                                 {
                                                                                   Item = item,
                                                                                   StationOrder = stationOrder
                                                                                 })
                                                   .Join(_dbContext.Orders.AsNoTracking(),
                                                         joined => joined.StationOrder.OrderId,
                                                         order => order.Id,
                                                         (joined, order) => new OrderItemOwnerRow
                                                                            {
                                                                              OrderItemId = joined.Item.Id,
                                                                              Order = order
                                                                            })
                                                   .ToListAsync(cancellationToken);

    return rows.ToDictionary(row => row.OrderItemId, row => row.Adapt<OrderItemOwner>(_mapperConfig));
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
                                      Items = itemsByOrder[header.OrderId].Select(row => new TableOrderRecordItem
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

    return _dbContext.OrderItems.AsNoTracking().Where(item => !stationOrderIdsOfAnotherFestival.Contains(item.StationOrderId));
  }
}
