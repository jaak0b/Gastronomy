using GastronomyApp.Core.Enums;

namespace GastronomyApp.Core.Services;

public sealed class OrderStatusCalculator
{
  public OrderStatus Calculate(int itemCount, int fulfilledItemCount)
  {
    if (fulfilledItemCount == 0)
      return OrderStatus.Open;

    if (fulfilledItemCount >= itemCount)
      return OrderStatus.Fulfilled;

    return OrderStatus.PartiallyFulfilled;
  }
}
