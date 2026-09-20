namespace GastronomyApp.Core.Results;

public sealed record CatalogItemAdministrationFailure
{
  public required CatalogItemAdministrationFailureReason Reason { get; init; }

  public double? OffendingProductionMinutes { get; init; }
}
