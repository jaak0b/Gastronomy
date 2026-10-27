using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Results;

public sealed record SearchedOrder
{
  public required Order Order { get; init; }

  public required IReadOnlyList<OrderItem> ShownItems { get; init; }
}
