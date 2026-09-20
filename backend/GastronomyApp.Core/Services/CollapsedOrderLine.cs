namespace GastronomyApp.Core.Services;

public sealed record CollapsedOrderLine<TLine>(TLine Line, int Quantity);
