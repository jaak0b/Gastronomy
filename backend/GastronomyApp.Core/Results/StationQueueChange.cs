using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Results;

public sealed record StationQueueChange
{
  public required Station Station { get; init; }

  public required IReadOnlyList<Order> ChangedOrders { get; init; }
}
