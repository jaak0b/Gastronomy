using GastronomyApp.Contracts.Admin.Festivals;

namespace GastronomyApp.Contracts.Catalog;

public sealed record CatalogView(RunningFestivalView? Festival, IReadOnlyList<CatalogCategoryView> Categories, IReadOnlyList<CatalogItemView> Items, IReadOnlyList<CatalogStationView> Stations)
{
  public static CatalogView Empty { get; } = new(null, [], [], []);
}
