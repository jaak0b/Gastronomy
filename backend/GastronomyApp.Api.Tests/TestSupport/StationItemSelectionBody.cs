namespace GastronomyApp.Api.Tests.TestSupport;

public sealed record StationItemSelectionBody(IReadOnlyList<Guid> OrderItemIds);
