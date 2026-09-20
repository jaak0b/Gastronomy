namespace GastronomyApp.Api.Tests.TestSupport;

public sealed record PlacedOrder(Guid OrderId, int GlobalOrderNumber, int TotalCents, int StationOrderCount, IReadOnlyList<int> SequenceNumbers);
