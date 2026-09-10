using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Repositories;

namespace GastronomyApp.Infrastructure.Tests.TestSupport;

public sealed class OrderAcceptanceComposition
{
  public OrderAcceptanceTransaction Create(GastronomyAppDbContext dbContext)
  {
    return Create(dbContext, new SequenceNumberAllocator(dbContext));
  }

  public OrderAcceptanceTransaction Create(GastronomyAppDbContext dbContext, INumberAllocator numberAllocator)
  {
    OrderRepository orderRepository = new(dbContext);

    OrderAcceptanceService acceptanceService = new(orderRepository,
                                                   new CatalogItemRepository(dbContext),
                                                   new StationRepository(dbContext),
                                                   new FestivalRepository(dbContext, new FestivalSchedule()),
                                                   numberAllocator,
                                                   new(),
                                                   new(),
                                                   new(),
                                                   new SystemClock());

    return new(dbContext, orderRepository, acceptanceService);
  }
}
