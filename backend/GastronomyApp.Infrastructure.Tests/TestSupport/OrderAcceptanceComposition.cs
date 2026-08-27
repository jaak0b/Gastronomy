using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Repositories;

namespace GastronomyApp.Infrastructure.Tests.TestSupport;

public sealed class OrderAcceptanceComposition
{
    public OrderAcceptanceTransaction Create(GastronomyAppDbContext dbContext)
    {
        OrderRepository orderRepository = new(dbContext);

        OrderAcceptanceService acceptanceService = new(
            orderRepository,
            new CatalogItemRepository(dbContext),
            new ProductionLocationRepository(dbContext),
            new NumberCounterAllocator(dbContext),
            new OrderRoutingResolver(),
            new OrderTotalCalculator(),
            new SystemClock());

        return new OrderAcceptanceTransaction(dbContext, orderRepository, acceptanceService);
    }
}
