using GastronomyApp.Core.Enums;

namespace GastronomyApp.Core.Services;

public sealed class ProductionStatusTransition
{
  public bool IsAllowed(ProductionStatus from, ProductionStatus to)
  {
    return to > from;
  }
}
