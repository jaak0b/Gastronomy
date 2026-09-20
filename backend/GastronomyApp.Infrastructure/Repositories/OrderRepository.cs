using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Infrastructure.Persistence;
using GastronomyApp.Infrastructure.QueryRows;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class OrderRepository : IOrderRepository
{
  private readonly GastronomyAppDbContext _dbContext;
  private readonly TypeAdapterConfig _mapperConfig;

  public OrderRepository(GastronomyAppDbContext dbContext, TypeAdapterConfig mapperConfig)
  {
    _dbContext = dbContext;
    _mapperConfig = mapperConfig;
  }

  public async Task<Order?> FindByClientOrderIdAsync(Guid clientOrderId, CancellationToken cancellationToken)
  {
    return await _dbContext.Orders.Include(order => order.StationOrders).ThenInclude(stationOrder => stationOrder.Items).FirstOrDefaultAsync(order => order.ClientOrderId == clientOrderId, cancellationToken);
  }

  public async Task<PlacedOrder?> FindPlacedAsync(Guid orderId, CancellationToken cancellationToken)
  {
    var order = await _dbContext.Orders.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == orderId, cancellationToken);

    if (order is null)
      return null;

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

  private async Task<IReadOnlyList<PlacedStationOrder>> FindStationOrdersAsync(Guid orderId, CancellationToken cancellationToken)
  {
    return await _dbContext.StationOrders.AsNoTracking()
                           .Where(stationOrder => stationOrder.OrderId == orderId)
                           .Join(_dbContext.Stations.AsNoTracking(),
                                 stationOrder => stationOrder.StationId,
                                 station => station.Id,
                                 (stationOrder, station) => new PlacedStationOrderRow
                                                            {
                                                              StationOrder = stationOrder,
                                                              Station = station
                                                            })
                           .OrderBy(row => row.Station.SortOrder)
                           .ThenBy(row => row.Station.Name)
                           .ThenBy(row => row.StationOrder.Id)
                           .ProjectToType<PlacedStationOrder>(_mapperConfig)
                           .ToListAsync(cancellationToken);
  }
}
