using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Services;

public sealed class OrderTotalCalculator
{
    public int CalculateTotalCents(IReadOnlyCollection<OrderLine> lines)
    {
        return lines.Sum(line => line.Quantity * line.UnitPriceCentsSnapshot);
    }
}
