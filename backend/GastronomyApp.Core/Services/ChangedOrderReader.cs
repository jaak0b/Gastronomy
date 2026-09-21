using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;

namespace GastronomyApp.Core.Services;

public sealed class ChangedOrderReader
{
  private readonly IStationOrderRepository _repository;

  public ChangedOrderReader(IStationOrderRepository repository)
  {
    _repository = repository;
  }

  public async Task<IReadOnlyList<Order>> ReadOrdersOfStationOrdersAsync(IReadOnlyCollection<Guid> stationOrderIds, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(stationOrderIds);

    if (stationOrderIds.Count == 0)
      return [];

    IReadOnlyList<Guid> orderIds = await _repository.FindOrderIdsOfStationOrdersAsync(stationOrderIds, cancellationToken);

    return await _repository.FindOrdersWithItemsAsync(orderIds, cancellationToken);
  }
}
