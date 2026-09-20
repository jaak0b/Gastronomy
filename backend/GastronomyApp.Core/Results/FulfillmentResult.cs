using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Results;

public sealed record FulfillmentResult
{
  public required IReadOnlyList<OrderItem> ChangedItems { get; init; }

  public required IReadOnlyList<OrderItem> AlreadyFulfilled { get; init; }
}
