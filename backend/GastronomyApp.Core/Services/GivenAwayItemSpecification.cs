using System.Linq.Expressions;
using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Services;

public sealed class GivenAwayItemSpecification
{
  public Expression<Func<OrderItem, bool>> WasGivenAwaySince(DateTime settledFromUtc)
  {
    return item => item.SettledAtUtc != null && item.SettledAtUtc >= settledFromUtc && item.ChargedPriceCents != null && item.ChargedPriceCents < item.UnitPriceCents;
  }
}
