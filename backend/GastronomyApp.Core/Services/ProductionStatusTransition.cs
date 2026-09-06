using GastronomyApp.Core.Enums;

namespace GastronomyApp.Core.Services;

public enum ProductionStatusStep
{
  Forward,
  AlreadyThere,
  Backwards
}

public sealed class ProductionStatusTransition
{
  public bool IsAllowed(ProductionStatus from, ProductionStatus to)
  {
    return to > from;
  }

  public ProductionStatusStep StepFrom(ProductionStatus from, ProductionStatus to)
  {
    if (from == to)
    {
      return ProductionStatusStep.AlreadyThere;
    }

    return IsAllowed(from, to) ? ProductionStatusStep.Forward : ProductionStatusStep.Backwards;
  }
}
