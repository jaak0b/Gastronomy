namespace GastronomyApp.Api.Contracts;

public sealed record AdminItemAtFestivalView(
  int PriceCents,
  bool IsAvailable,
  IReadOnlyList<Guid> StationIds);
