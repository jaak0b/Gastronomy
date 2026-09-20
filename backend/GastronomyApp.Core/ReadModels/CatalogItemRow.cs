namespace GastronomyApp.Core.ReadModels;

public sealed record CatalogItemRow(
  Guid ItemId,
  Guid CategoryId,
  string Name,
  int PriceCents,
  int SortOrder,
  bool IsAvailable,
  double? ProductionMinutes,
  bool IsQueueIndependent,
  IReadOnlyList<Guid> StationIds);
