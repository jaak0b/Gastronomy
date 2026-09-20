using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GastronomyApp.Infrastructure.Tests.TestSupport;

public sealed class OrderAcceptanceComposition
{
  public OrderAcceptanceService Create(GastronomyAppDbContext dbContext)
  {
    return Create(dbContext, new SequenceNumberAllocator(dbContext));
  }

  public OrderAcceptanceService Create(GastronomyAppDbContext dbContext, INumberAllocator numberAllocator)
  {
    return Create(dbContext, numberAllocator, NullLogger<ImmediateTransactionRunner>.Instance);
  }

  public OrderAcceptanceService Create(GastronomyAppDbContext dbContext,
                                       INumberAllocator numberAllocator,
                                       ILogger<ImmediateTransactionRunner> logger)
  {
    OrderItemResolutionService itemResolutionService = new(new CatalogItemRepository(dbContext),
                                                           new StationRepository(dbContext),
                                                           new());

    return new(new OrderRepository(dbContext),
               new FestivalRepository(dbContext, new FestivalSchedule()),
               numberAllocator,
               itemResolutionService,
               new(),
               new ImmediateTransactionRunner(dbContext, new(), logger),
               new SystemClock());
  }
}
