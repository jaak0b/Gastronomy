using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Results;

public enum SettlementFailureReason
{
  NoItemsSelected,
  PaymentNoticeMissing,
  UnknownOrderItemId,
  AmountPaidMissing,
  AmountPaidNegative,
  SelectionSpansSeveralTables,
  NoRunningFestival,
  DuplicateOrderItemId
}

public sealed record SettlementFailure
{
  public required SettlementFailureReason Reason { get; init; }

  public Guid? OffendingOrderItemId { get; init; }

  public IReadOnlyList<string> TableNamesInTheSelection { get; init; } = [];
}

public sealed record SettlementLine
{
  public required Guid OrderItemId { get; init; }

  public required int? PaidPriceCents { get; init; }

  public string? PaymentNotice { get; init; }
}

public sealed record SettlementRequest
{
  public required IReadOnlyList<SettlementLine> Lines { get; init; }

  public required Guid SettledByStaffMemberId { get; init; }
}

public sealed record SettlementResult
{
  public required IReadOnlyList<OrderItem> NewlySettled { get; init; }

  public required IReadOnlyList<OrderItem> Reapplied { get; init; }

  public required IReadOnlyList<OrderItem> AlreadySettledByOthers { get; init; }
}
