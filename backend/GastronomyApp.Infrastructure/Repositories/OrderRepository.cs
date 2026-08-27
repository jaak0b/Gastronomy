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
            .Include(order => order.Lines)
            .Include(order => order.Tickets)
            .FirstOrDefaultAsync(order => order.ClientOrderId == clientOrderId, cancellationToken);
    }

    public async Task AddAsync(Order order, CancellationToken cancellationToken)
    {
        int recomputedTotalCents = order.Lines.Sum(line => line.Quantity * line.UnitPriceCentsSnapshot);
        if (recomputedTotalCents != order.TotalCents)
        {
            throw new InvalidOperationException(
                $"The order total {order.TotalCents} disagrees with the sum of its lines {recomputedTotalCents}.");
        }

        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
