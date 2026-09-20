namespace GastronomyApp.Core.Results;

public sealed record CatalogCategoryAdministrationFailure
{
  public required CatalogCategoryAdministrationFailureReason Reason { get; init; }
}
