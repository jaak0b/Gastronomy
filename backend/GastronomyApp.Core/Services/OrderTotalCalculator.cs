using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Services;

public sealed class OrderTotalCalculator
{
  public int SumTotalCents(Order order)
  {
    ArgumentNullException.ThrowIfNull(order);

    return order.StationOrders.SelectMany(stationOrder => stationOrder.Items).Sum(item => item.UnitPriceCents);
  }
}
