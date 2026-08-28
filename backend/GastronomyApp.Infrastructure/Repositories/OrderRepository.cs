using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
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
            .Include(order => order.StationOrders)
                .ThenInclude(stationOrder => stationOrder.PrintJobs)
            .FirstOrDefaultAsync(order => order.ClientOrderId == clientOrderId, cancellationToken);
    }

    public async Task AddAsync(Order order, CancellationToken cancellationToken)
    {
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
