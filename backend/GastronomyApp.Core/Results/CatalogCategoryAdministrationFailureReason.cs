namespace GastronomyApp.Core.Results;

public enum CatalogCategoryAdministrationFailureReason
{
  CategoryNotFound,
  NameMissing,
  NameTaken,
  ColourInvalid,
  CategoryHoldsActiveItems
}
