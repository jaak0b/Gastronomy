using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Ports;

public interface IStationOrderRepository
{
  public Task<Station?> FindStationWithUnfinishedOrdersAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken);

  public Task<IReadOnlyList<StationOrder>> FindFulfilledAtStationAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken);

  public Task<IReadOnlyList<OrderItem>> FindItemsAtStationAsync(IReadOnlyCollection<Guid> orderItemIds, Guid stationId, CancellationToken cancellationToken);

  public Task<StationOrder?> FindAtStationAsync(Guid stationOrderId, Guid stationId, Guid festivalId, CancellationToken cancellationToken);

  public Task<IReadOnlyList<Guid>> FindOrderIdsOfStationOrdersAsync(IReadOnlyCollection<Guid> stationOrderIds, CancellationToken cancellationToken);

  public Task<IReadOnlyList<Order>> FindOrdersWithItemsAsync(IReadOnlyCollection<Guid> orderIds, CancellationToken cancellationToken);

  public Task SaveChangesAsync(CancellationToken cancellationToken);
}
