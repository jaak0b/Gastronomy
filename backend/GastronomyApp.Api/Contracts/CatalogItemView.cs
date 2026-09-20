namespace GastronomyApp.Api.Contracts;

public sealed record CatalogItemView(Guid Id, Guid CategoryId, string Name, int PriceCents, int SortOrder, bool IsAvailable, double? ProductionMinutes, bool IsQueueIndependent, IReadOnlyList<Guid> StationIds);
