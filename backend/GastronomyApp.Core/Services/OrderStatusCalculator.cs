using GastronomyApp.Core.Enums;

namespace GastronomyApp.Core.Services;

public sealed class OrderStatusCalculator
{
  public OrderStatus Calculate(int itemCount, int fulfilledItemCount)
  {
    if (fulfilledItemCount == 0)
    {
      return OrderStatus.Open;
    }

    return fulfilledItemCount >= itemCount ? OrderStatus.Fulfilled : OrderStatus.PartiallyFulfilled;
  }
}
