namespace GastronomyApp.Contracts.Orders;

public sealed record StationQuoteView(Guid StationId, double? ReadyInMinutes);
