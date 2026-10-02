using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Persistence;
using GastronomyApp.Infrastructure.Repositories;
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
    OrderItemResolutionService itemResolutionService = new(new CatalogItemRepository(dbContext), new StationRepository(dbContext), new());

    FestivalRepository festivalRepository = new(dbContext, new());
    RunningFestivalLookup runningFestival = new(festivalRepository, new(), TimeProvider.System);

    StockSoldOutMarker soldOutMarker = new(new IngredientStockRepository(dbContext), NullLogger<StockSoldOutMarker>.Instance);

    return new(new OrderRepository(dbContext), runningFestival, numberAllocator, itemResolutionService, soldOutMarker, TimeProvider.System, NullLogger<OrderAcceptanceService>.Instance);
  }
}
