namespace GastronomyApp.Contracts.Admin.Catalog;

public sealed record AdminItemAtFestivalView(int PriceCents, bool IsAvailable, IReadOnlyList<Guid> StationIds);
