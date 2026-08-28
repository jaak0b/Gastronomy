using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Repositories;

namespace GastronomyApp.Infrastructure.Tests.TestSupport;

public sealed class OrderAcceptanceComposition
{
  public OrderAcceptanceTransaction Create(GastronomyAppDbContext dbContext)
  {
    OrderRepository orderRepository = new(dbContext);

    OrderAcceptanceService acceptanceService = new(orderRepository,
                                                   new CatalogItemRepository(dbContext),
                                                   new StationRepository(dbContext),
                                                   new SequenceNumberAllocator(dbContext),
                                                   new(),
                                                   new SystemClock());

    return new(dbContext, orderRepository, acceptanceService);
  }
}
