using GastronomyApp.Core.Enums;

namespace GastronomyApp.Core.ReadModels;

public sealed record PlacedOrderReport
{
  public required PlacedOrder Order { get; init; }

  public required OrderStatus Status { get; init; }

  public required int TotalCents { get; init; }
}
