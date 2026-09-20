using GastronomyApp.Core.Entities;

namespace GastronomyApp.Infrastructure.QueryRows;

public sealed record FestivalContentCountsRow
{
  public required Festival Festival { get; init; }

  public required int StationCount { get; init; }

  public required int MenuItemCount { get; init; }

  public required int OrderCount { get; init; }
}
