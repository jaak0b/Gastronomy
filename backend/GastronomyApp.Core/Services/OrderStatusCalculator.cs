using GastronomyApp.Core.Enums;

namespace GastronomyApp.Core.Services;

public sealed class OrderStatusCalculator
{
  public OrderStatus Calculate(IReadOnlyCollection<ProductionStatus> itemStatuses)
  {
    ArgumentNullException.ThrowIfNull(itemStatuses);

    if (itemStatuses.Count == 0)
    {
      return OrderStatus.Waiting;
    }

    if (itemStatuses.All(status => status == ProductionStatus.Finished))
    {
      return OrderStatus.Finished;
    }

    return itemStatuses.All(status => status == ProductionStatus.Waiting)
             ? OrderStatus.Waiting
             : OrderStatus.InProduction;
  }
}
