using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.ReadModels;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class StationOrderRepository : IStationOrderRepository
{
  private readonly GastronomyAppDbContext _dbContext;

  public StationOrderRepository(GastronomyAppDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task<IReadOnlyList<QueuedStationOrder>> FindUnfinishedAtStationAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken)
  {
    IQueryable<StationOrder> stationOrders = StationOrdersAt(stationId).Where(stationOrder => stationOrder.Items.Any(item => item.FulfilledAtUtc == null));

    return await BuildQueuedStationOrders(stationOrders, festivalId).ToListAsync(cancellationToken);
  }

  public async Task<IReadOnlyList<QueuedStationOrder>> FindFulfilledAtStationAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken)
  {
    IQueryable<StationOrder> stationOrders = StationOrdersAt(stationId).Where(stationOrder => stationOrder.Items.Any(item => item.FulfilledAtUtc != null));

    return await BuildQueuedStationOrders(stationOrders, festivalId).ToListAsync(cancellationToken);
  }

  public async Task<IReadOnlyList<OrderItem>> FindItemsAtStationAsync(IReadOnlyCollection<Guid> orderItemIds, Guid stationId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(orderItemIds);

    List<Guid> ids = orderItemIds.ToList();

    List<Guid> stationOrderIdsAtThisStation = await StationOrdersAt(stationId).Select(stationOrder => stationOrder.Id).ToListAsync(cancellationToken);

    return await _dbContext.OrderItems.Where(item => ids.Contains(item.Id) && stationOrderIdsAtThisStation.Contains(item.StationOrderId)).ToListAsync(cancellationToken);
  }

  public async Task<StationOrder?> FindAtStationAsync(Guid stationOrderId, Guid stationId, Guid festivalId, CancellationToken cancellationToken)
  {
    return await _dbContext.StationOrders.FirstOrDefaultAsync(stationOrder => stationOrder.Id == stationOrderId && stationOrder.StationId == stationId && stationOrder.FestivalId == festivalId, cancellationToken);
  }

  public async Task<IReadOnlyList<Guid>> FindOrderIdsOfStationOrdersAsync(IReadOnlyCollection<Guid> stationOrderIds, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(stationOrderIds);

    List<Guid> ids = stationOrderIds.ToList();

    return await _dbContext.StationOrders.AsNoTracking().Where(stationOrder => ids.Contains(stationOrder.Id)).Select(stationOrder => stationOrder.OrderId).Distinct().ToListAsync(cancellationToken);
  }

  public async Task<IReadOnlyList<OrderFulfillmentCounts>> FindFulfillmentCountsAsync(IReadOnlyCollection<Guid> orderIds, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(orderIds);

    List<Guid> ids = orderIds.ToList();

    return await _dbContext.Orders.AsNoTracking()
                           .Where(order => ids.Contains(order.Id))
                           .Select(order => new OrderFulfillmentCounts
                                            {
                                              OrderId = order.Id,
                                              ItemCount = order.StationOrders.SelectMany(stationOrder => stationOrder.Items).Count(),
                                              FulfilledItemCount = order.StationOrders.SelectMany(stationOrder => stationOrder.Items).Count(item => item.FulfilledAtUtc != null)
                                            })
                           .ToListAsync(cancellationToken);
  }

  public async Task<IReadOnlyList<StationQueuedWork>> FindQueuedWorkAtFestivalAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    return await _dbContext.OrderItems.AsNoTracking()
                           .Where(item => item.FulfilledAtUtc == null)
                           .Join(_dbContext.StationOrders.AsNoTracking().Where(stationOrder => stationOrder.FestivalId == festivalId),
                                 item => item.StationOrderId,
                                 stationOrder => stationOrder.Id,
                                 (item, stationOrder) => new
                                                         {
                                                           Item = item,
                                                           StationOrder = stationOrder
                                                         })
                           .Join(_dbContext.CatalogItems.AsNoTracking(),
                                 joined => joined.Item.CatalogItemId,
                                 catalogItem => catalogItem.Id,
                                 (joined, catalogItem) => new StationQueuedWork
                                                          {
                                                            StationId = joined.StationOrder.StationId,
                                                            Work = new(catalogItem.ProductionMinutes, catalogItem.IsQueueIndependent)
                                                          })
                           .ToListAsync(cancellationToken);
  }

  public async Task SaveChangesAsync(CancellationToken cancellationToken)
  {
    await _dbContext.SaveChangesAsync(cancellationToken);
  }

  private IQueryable<StationOrder> StationOrdersAt(Guid stationId)
  {
    return _dbContext.StationOrders.AsNoTracking().Where(stationOrder => stationOrder.StationId == stationId);
  }

  private IQueryable<QueuedStationOrder> BuildQueuedStationOrders(IQueryable<StationOrder> stationOrders, Guid festivalId)
  {
    return stationOrders.Join(_dbContext.Orders.AsNoTracking(),
                              stationOrder => stationOrder.OrderId,
                              order => order.Id,
                              (stationOrder, order) => new
                                                       {
                                                         StationOrder = stationOrder,
                                                         Order = order
                                                       })
                        .Where(joined => joined.Order.FestivalId == festivalId)
                        .OrderBy(joined => joined.StationOrder.StationOrderNumber)
                        .Select(joined => new QueuedStationOrder
                                          {
                                            StationOrderId = joined.StationOrder.Id,
                                            GlobalOrderNumber = joined.Order.GlobalOrderNumber,
                                            StationOrderNumber = joined.StationOrder.StationOrderNumber,
                                            TableName = joined.Order.TableName,
                                            Note = joined.Order.Note,
                                            DeliveryMode = joined.StationOrder.DeliveryMode,
                                            CreatedAtUtc = joined.Order.CreatedAtUtc,
                                            IsHiddenFromAsItComesQueue = joined.StationOrder.IsHiddenFromAsItComesQueue,
                                            ItemCount = joined.StationOrder.Items.Count,
                                            FulfilledItemCount = joined.StationOrder.Items.Count(item => item.FulfilledAtUtc != null),
                                            Items = joined.StationOrder.Items.OrderBy(item => item.Id)
                                                          .Select(item => new QueuedOrderItem
                                                                          {
                                                                            OrderItemId = item.Id,
                                                                            ItemName = item.ItemName,
                                                                            Note = item.Note,
                                                                            FulfilledAtUtc = item.FulfilledAtUtc
                                                                          })
                                                          .ToList()
                                          });
  }
}
