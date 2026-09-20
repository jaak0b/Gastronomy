using GastronomyApp.Core.Ports;
using GastronomyApp.Core.ReadModels;

namespace GastronomyApp.Core.Services;

public sealed class PlacedOrderReader
{
  private readonly IOrderRepository _orderRepository;
  private readonly OrderStatusCalculator _statusCalculator;
  private readonly OrderTotalCalculator _totalCalculator;

  public PlacedOrderReader(IOrderRepository orderRepository,
                           OrderStatusCalculator statusCalculator,
                           OrderTotalCalculator totalCalculator)
  {
    _orderRepository = orderRepository;
    _statusCalculator = statusCalculator;
    _totalCalculator = totalCalculator;
  }

  public async Task<PlacedOrderReport?> FindAsync(Guid orderId, CancellationToken cancellationToken)
  {
    PlacedOrder? placedOrder = await _orderRepository.FindPlacedAsync(orderId, cancellationToken);

    if (placedOrder is null)
    {
      return null;
    }

    List<PlacedOrderItem> items = [.. placedOrder.StationOrders.SelectMany(stationOrder => stationOrder.Items)];

    return new()
           {
             Order = placedOrder,
             Status = _statusCalculator.Calculate(items.Count, items.Count(item => item.IsFulfilled)),
             TotalCents = _totalCalculator.SumTotalCents(items)
           };
  }
}
