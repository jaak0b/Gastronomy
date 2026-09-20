using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.ReadModels;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class OrderRepository : IOrderRepository
{
  private readonly GastronomyAppDbContext _dbContext;

  public OrderRepository(GastronomyAppDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task<Order?> FindByClientOrderIdAsync(Guid clientOrderId, CancellationToken cancellationToken)
  {
    return await _dbContext.Orders
                           .Include(order => order.StationOrders)
                           .ThenInclude(stationOrder => stationOrder.Items)
                           .FirstOrDefaultAsync(order => order.ClientOrderId == clientOrderId, cancellationToken);
  }

  public async Task<PlacedOrder?> FindPlacedAsync(Guid orderId, CancellationToken cancellationToken)
  {
    Order? order = await _dbContext.Orders
                                   .AsNoTracking()
                                   .FirstOrDefaultAsync(candidate => candidate.Id == orderId, cancellationToken);

    if (order is null)
    {
      return null;
    }

    return new()
           {
             OrderId = order.Id,
             GlobalOrderNumber = order.GlobalOrderNumber,
             CreatedAtUtc = order.CreatedAtUtc,
             StationOrders = await FindStationOrdersAsync(order.Id, cancellationToken)
           };
  }

  public async Task AddAsync(Order order, CancellationToken cancellationToken)
  {
    _dbContext.Orders.Add(order);
    await _dbContext.SaveChangesAsync(cancellationToken);
  }

  private async Task<IReadOnlyList<PlacedStationOrder>> FindStationOrdersAsync(Guid orderId,
                                                                               CancellationToken cancellationToken)
  {
    return await _dbContext.StationOrders
                           .AsNoTracking()
                           .Where(stationOrder => stationOrder.OrderId == orderId)
                           .Join(_dbContext.Stations.AsNoTracking(),
                                 stationOrder => stationOrder.StationId,
                                 station => station.Id,
                                 (stationOrder, station) => new { StationOrder = stationOrder, Station = station })
                           .OrderBy(joined => joined.StationOrder.StationOrderNumber)
                           .ThenBy(joined => joined.StationOrder.Id)
                           .Select(joined => new PlacedStationOrder
                                             {
                                               StationOrderId = joined.StationOrder.Id,
                                               StationId = joined.StationOrder.StationId,
                                               StationName = joined.Station.Name,
                                               StationOrderNumber = joined.StationOrder.StationOrderNumber,
                                               DeliveryMode = joined.StationOrder.DeliveryMode,
                                               Items = _dbContext.OrderItems
                                                                 .AsNoTracking()
                                                                 .Where(item => item.StationOrderId == joined.StationOrder.Id)
                                                                 .OrderBy(item => item.Id)
                                                                 .Select(item => new PlacedOrderItem
                                                                                 {
                                                                                   OrderItemId = item.Id,
                                                                                   UnitPriceCents = item.UnitPriceCents,
                                                                                   IsFulfilled = item.FulfilledAtUtc != null
                                                                                 })
                                                                 .ToList()
                                             })
                           .ToListAsync(cancellationToken);
  }
}
