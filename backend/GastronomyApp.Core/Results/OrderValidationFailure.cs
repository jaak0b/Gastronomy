namespace GastronomyApp.Core.Results;

public sealed record OrderValidationFailure
{
  public required OrderValidationFailureReason Reason { get; init; }

  public Guid? OffendingCatalogItemId { get; init; }

  public string? OffendingCatalogItemName { get; init; }

  public SettlementFailureReason? SettlementFailureReason { get; init; }
}
