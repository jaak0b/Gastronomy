using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Infrastructure.Persistence;
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
    return await _dbContext.Orders.Include(order => order.StationOrders).ThenInclude(stationOrder => stationOrder.Items).FirstOrDefaultAsync(order => order.ClientOrderId == clientOrderId, cancellationToken);
  }

  public async Task<Order?> FindWithStationOrdersAsync(Guid orderId, CancellationToken cancellationToken)
  {
    return await _dbContext.Orders.AsNoTracking()
                           .Include(order => order.StationOrders)
                           .ThenInclude(stationOrder => stationOrder.Station)
                           .Include(order => order.StationOrders)
                           .ThenInclude(stationOrder => stationOrder.Items)
                           .ThenInclude(item => item.CatalogItem)
                           .FirstOrDefaultAsync(order => order.Id == orderId, cancellationToken);
  }

  public async Task AddAsync(Order order, CancellationToken cancellationToken)
  {
    _dbContext.Orders.Add(order);
    await _dbContext.SaveChangesAsync(cancellationToken);
  }
}
