using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Persistence;
using GastronomyApp.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

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

  public OrderAcceptanceService Create(GastronomyAppDbContext dbContext, INumberAllocator numberAllocator, ILogger<ImmediateTransactionRunner> logger)
  {
    OrderItemResolutionService itemResolutionService = new(new CatalogItemRepository(dbContext), new StationRepository(dbContext, new FakeTimeProvider(new(2026, 8, 27, 18, 0, 0, TimeSpan.Zero))), new());

    ImmediateTransactionRunner transactionRunner = new(dbContext, new(), new(), logger);
    FestivalRepository festivalRepository = new(dbContext, new());
    RunningFestivalLookup runningFestival = new(festivalRepository, new(), TimeProvider.System);

    return new(new OrderRepository(dbContext), runningFestival, numberAllocator, itemResolutionService, new(new OpenItemRepository(dbContext), runningFestival, transactionRunner, TimeProvider.System), transactionRunner, TimeProvider.System);
  }
}
