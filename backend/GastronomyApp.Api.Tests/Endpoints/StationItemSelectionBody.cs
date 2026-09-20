namespace GastronomyApp.Api.Tests.Endpoints;

public sealed record StationItemSelectionBody(IReadOnlyList<Guid> OrderItemIds);
