using GastronomyApp.Core.Entities;

namespace GastronomyApp.Infrastructure.QueryRows;

public sealed record PlacedStationOrderRow
{
  public required StationOrder StationOrder { get; init; }

  public required Station Station { get; init; }
}
