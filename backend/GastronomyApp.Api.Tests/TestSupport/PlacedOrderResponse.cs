namespace GastronomyApp.Api.Tests.TestSupport;

public sealed record PlacedOrderResponse(Guid OrderId, int GlobalOrderNumber, int TotalCents, int StationOrderCount, IReadOnlyList<int> SequenceNumbers);
