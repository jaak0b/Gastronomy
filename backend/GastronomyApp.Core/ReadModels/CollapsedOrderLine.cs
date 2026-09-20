namespace GastronomyApp.Core.ReadModels;

public sealed record CollapsedOrderLine<TLine>(TLine Line, int Quantity);
