using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Results;

public sealed record StationAtFestival
{
  public required Station Station { get; init; }

  public required Guid FestivalId { get; init; }
}
