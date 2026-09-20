namespace GastronomyApp.Core.ReadModels;

public sealed record AdministeredCatalogItem(
  Guid ItemId,
  string Name,
  Guid CategoryId,
  int SortOrder,
  bool IsActive,
  double? ProductionMinutes,
  bool IsQueueIndependent,
  CatalogItemAtFestival? AtTheFestival);
