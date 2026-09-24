namespace GastronomyApp.Contracts.Orders;

public sealed record ItemEstimateView(Guid CatalogItemId, Guid StationId, double ReadyInMinutes);
