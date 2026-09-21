using GastronomyApp.Core.Entities;
using GastronomyApp.Core.ReadModels;

namespace GastronomyApp.Core.Results;

public sealed record StationQueueChange
{
  public required StationQueue Queue { get; init; }

  public required IReadOnlyList<Order> ChangedOrders { get; init; }
}
