using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Results;

public sealed record SettlementResult
{
  public required IReadOnlyList<OrderItem> NewlySettled { get; init; }

  public required IReadOnlyList<OrderItem> Reapplied { get; init; }

  public required IReadOnlyList<OrderItem> AlreadySettledByOthers { get; init; }
}
