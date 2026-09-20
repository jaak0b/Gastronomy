namespace GastronomyApp.Core.ReadModels;

public sealed record CatalogItemAtFestival(int PriceCents, bool IsAvailable, IReadOnlyList<Guid> StationIds);
