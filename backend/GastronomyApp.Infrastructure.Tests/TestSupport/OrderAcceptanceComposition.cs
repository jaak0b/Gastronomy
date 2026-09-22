using FakeItEasy;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Persistence;
using GastronomyApp.Infrastructure.Repositories;
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
    OrderItemResolutionService itemResolutionService = new(new CatalogItemRepository(dbContext), new StationRepository(dbContext, new FakeTimeProvider(new(2026, 8, 27, 18, 0, 0, TimeSpan.Zero)), new()), new());

    AfterCommitActions afterCommitActions = new();
    FestivalRepository festivalRepository = new(dbContext, new());
    RunningFestivalLookup runningFestival = new(festivalRepository, new(), TimeProvider.System);
    OrderItemSettlementService settlementService = new(new OpenItemRepository(dbContext), runningFestival, A.Fake<ISettlementAnnouncer>(), afterCommitActions, TimeProvider.System, NullLogger<OrderItemSettlementService>.Instance);

    return new(new OrderRepository(dbContext), runningFestival, numberAllocator, itemResolutionService, settlementService, A.Fake<IStationOrdersAnnouncer>(), afterCommitActions, TimeProvider.System);
  }
}
