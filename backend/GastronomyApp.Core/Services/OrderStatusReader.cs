using GastronomyApp.Core.Ports;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class OrderStatusReader
{
  private readonly IStationOrderRepository _repository;
  private readonly OrderStatusCalculator _statusCalculator;

  public OrderStatusReader(IStationOrderRepository repository, OrderStatusCalculator statusCalculator)
  {
    _repository = repository;
    _statusCalculator = statusCalculator;
  }

  public async Task<IReadOnlyList<OrderStatusChange>> ReadStatusesOfStationOrdersAsync(
    IReadOnlyCollection<Guid> stationOrderIds,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(stationOrderIds);

    if (stationOrderIds.Count == 0)
    {
      return [];
    }

    IReadOnlyList<Guid> orderIds =
      await _repository.FindOrderIdsOfStationOrdersAsync(stationOrderIds, cancellationToken);

    IReadOnlyList<OrderFulfillmentCounts> counts =
      await _repository.FindFulfillmentCountsAsync(orderIds, cancellationToken);

    return
    [
      .. counts.Select(count => new OrderStatusChange
                                {
                                  OrderId = count.OrderId,
                                  Status = _statusCalculator.Calculate(count.ItemCount, count.FulfilledItemCount)
                                })
    ];
  }
}
