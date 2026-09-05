using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Results;

public enum SettlementKind
{
  AtTheDisplayedPrice,
  FreeOfCharge
}

public enum SettlementFailureReason
{
  NoItemsSelected,
  TooManyItemsSelected,
  PaymentNoticeMissing,
  PaymentNoticeTooLong,
  UnknownOrderItemId
}

public sealed record SettlementFailure
{
  public required SettlementFailureReason Reason { get; init; }

  public Guid? OffendingOrderItemId { get; init; }
}

public sealed record SettlementResult
{
  public required IReadOnlyList<OrderItem> NewlySettled { get; init; }

  public required IReadOnlyList<OrderItem> AlreadySettledBeforehand { get; init; }
}
