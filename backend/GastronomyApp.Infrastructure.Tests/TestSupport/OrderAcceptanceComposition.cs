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

    ImmediateTransactionRunner transactionRunner = new(dbContext, new(), logger);
    FestivalRepository festivalRepository = new(dbContext, new FestivalSchedule());

    return new(new OrderRepository(dbContext),
               festivalRepository,
               numberAllocator,
               itemResolutionService,
               new(new OpenItemRepository(dbContext, new()), festivalRepository, transactionRunner, new SystemClock()),
               transactionRunner,
               new SystemClock());
  }
}
