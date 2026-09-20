namespace GastronomyApp.Core.Results;

public sealed record SettlementFailure
{
  public required SettlementFailureReason Reason { get; init; }

  public Guid? OffendingOrderItemId { get; init; }

  public IReadOnlyList<string> TableNamesInTheSelection { get; init; } = [];
}
