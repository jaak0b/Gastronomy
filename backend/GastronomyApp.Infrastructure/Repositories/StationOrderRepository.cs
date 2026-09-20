using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Infrastructure.Persistence;
using GastronomyApp.Infrastructure.QueryRows;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class StationOrderRepository : IStationOrderRepository
{
  private readonly GastronomyAppDbContext _dbContext;
  private readonly TypeAdapterConfig _mapperConfig;

  public StationOrderRepository(GastronomyAppDbContext dbContext, TypeAdapterConfig mapperConfig)
  {
    _dbContext = dbContext;
    _mapperConfig = mapperConfig;
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

    return await _dbContext.Orders.AsNoTracking().Where(order => ids.Contains(order.Id)).ProjectToType<OrderFulfillmentCounts>(_mapperConfig).ToListAsync(cancellationToken);
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
                                 (joined, catalogItem) => new StationQueuedWorkRow
                                                          {
                                                            StationOrder = joined.StationOrder,
                                                            CatalogItem = catalogItem
                                                          })
                           .ProjectToType<StationQueuedWork>(_mapperConfig)
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
                        .Join(_dbContext.StaffMembers.AsNoTracking(),
                              row => row.Order.StaffMemberId,
                              staffMember => staffMember.Id,
                              (row, staffMember) => new QueuedStationOrderRow
                                                    {
                                                      StationOrder = row.StationOrder,
                                                      Order = row.Order,
                                                      StaffMemberName = staffMember.Name
                                                    })
                        .Where(row => row.Order.FestivalId == festivalId)
                        .OrderBy(row => row.StationOrder.StationOrderNumber)
                        .ProjectToType<QueuedStationOrder>(_mapperConfig);
  }
}
