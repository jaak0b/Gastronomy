using GastronomyApp.Core.ReadModels;

namespace GastronomyApp.Core.Services;

public sealed class OrderTotalCalculator
{
  public int SumTotalCents(IEnumerable<PlacedOrderItem> items)
  {
    ArgumentNullException.ThrowIfNull(items);

    return items.Sum(item => item.UnitPriceCents);
  }
}
