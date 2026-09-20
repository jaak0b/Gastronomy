using GastronomyApp.Core.Entities;
using GastronomyApp.Core.ReadModels;

namespace GastronomyApp.Core.Ports;

public interface IStationOrderRepository
{
  public Task<IReadOnlyList<QueuedStationOrder>> FindUnfinishedAtStationAsync(Guid festivalId,
                                                                              Guid stationId,
                                                                              CancellationToken cancellationToken);

  public Task<IReadOnlyList<QueuedStationOrder>> FindFulfilledAtStationAsync(Guid festivalId,
                                                                             Guid stationId,
                                                                             CancellationToken cancellationToken);

  public Task<IReadOnlyList<OrderItem>> FindItemsAtStationAsync(IReadOnlyCollection<Guid> orderItemIds,
                                                                 Guid stationId,
                                                                 CancellationToken cancellationToken);

  public Task<StationOrder?> FindAtStationAsync(Guid stationOrderId,
                                                 Guid stationId,
                                                 Guid festivalId,
                                                 CancellationToken cancellationToken);

  public Task<IReadOnlyList<Guid>> FindOrderIdsOfStationOrdersAsync(IReadOnlyCollection<Guid> stationOrderIds,
                                                                     CancellationToken cancellationToken);

  public Task<IReadOnlyList<OrderFulfillmentCounts>> FindFulfillmentCountsAsync(IReadOnlyCollection<Guid> orderIds,
                                                                                 CancellationToken cancellationToken);

  public Task<IReadOnlyList<StationQueuedWork>> FindQueuedWorkAtFestivalAsync(Guid festivalId,
                                                                               CancellationToken cancellationToken);

  public Task SaveChangesAsync(CancellationToken cancellationToken);
}
