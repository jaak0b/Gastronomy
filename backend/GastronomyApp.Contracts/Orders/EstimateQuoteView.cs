namespace GastronomyApp.Contracts.Orders;

public sealed record EstimateQuoteView(IReadOnlyList<StationQuoteView> Stations);
