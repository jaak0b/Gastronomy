namespace GastronomyApp.Api.Contracts;

public sealed record CatalogCategoryView(Guid CategoryId, string Name, string ColourHex, int SortOrder);

public sealed record CatalogItemView(
  Guid Id,
  Guid CategoryId,
  string Name,
  int PriceCents,
  int SortOrder,
  bool IsAvailable,
  int? ProductionMinutes,
  IReadOnlyList<Guid> StationIds);

public sealed record CatalogStationView(Guid Id, string Name, int SortOrder);

public sealed record RunningFestivalView(Guid FestivalId, string Name);

public sealed record CatalogView(
  RunningFestivalView? Festival,
  IReadOnlyList<CatalogCategoryView> Categories,
  IReadOnlyList<CatalogItemView> Items,
  IReadOnlyList<CatalogStationView> Stations);
