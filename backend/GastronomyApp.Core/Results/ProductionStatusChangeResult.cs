using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Results;

public enum ProductionStatusFailureReason
{
  NoItemsSelected,
  UnknownOrderItemId,
  TransitionNotAllowed
}

public sealed record ProductionStatusFailure
{
  public required ProductionStatusFailureReason Reason { get; init; }

  public Guid? OffendingOrderItemId { get; init; }
}

public sealed record ProductionStatusChangeResult
{
  public required IReadOnlyList<OrderItem> ChangedItems { get; init; }
}
