namespace GastronomyApp.Api.Tests.Endpoints;

public sealed record PlacedOrder(
  Guid OrderId,
  int GlobalOrderNumber,
  int TotalCents,
  int StationOrderCount,
  IReadOnlyList<int> SequenceNumbers);
