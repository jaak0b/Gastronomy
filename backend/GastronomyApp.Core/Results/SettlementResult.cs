using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Results;

public enum SettlementFailureReason
{
  NoItemsSelected,
  TooManyItemsSelected,
  PaymentNoticeMissing,
  UnknownOrderItemId,
  AmountPaidMissing,
  AmountPaidNegative,
  SelectionSpansSeveralTables
}

public sealed record SettlementFailure
{
  public required SettlementFailureReason Reason { get; init; }

  public Guid? OffendingOrderItemId { get; init; }

  public IReadOnlyList<string> TableNamesInTheSelection { get; init; } = [];
}

public sealed record SettlementResult
{
  public required IReadOnlyList<OrderItem> NewlySettled { get; init; }

  public required IReadOnlyList<OrderItem> AlreadySettledBeforehand { get; init; }
}
