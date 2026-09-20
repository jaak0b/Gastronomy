namespace GastronomyApp.Core.ReadModels;

public sealed record CatalogAtFestival(
  Guid FestivalId,
  string FestivalName,
  IReadOnlyList<CatalogStationRow> Stations,
  IReadOnlyList<CatalogCategoryRow> Categories,
  IReadOnlyList<CatalogItemRow> Items);
