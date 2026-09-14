using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Results;

public enum FulfillmentFailureReason
{
  NoItemsSelected,
  UnknownOrderItemId,
  ItemNotFulfilled
}

public sealed record FulfillmentFailure
{
  public required FulfillmentFailureReason Reason { get; init; }

  public Guid? OffendingOrderItemId { get; init; }
}

public sealed record FulfillmentResult
{
  public required IReadOnlyList<OrderItem> ChangedItems { get; init; }

  public required IReadOnlyList<OrderItem> AlreadyFulfilled { get; init; }
}
