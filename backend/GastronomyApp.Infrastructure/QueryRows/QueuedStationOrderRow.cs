using GastronomyApp.Core.Entities;

namespace GastronomyApp.Infrastructure.QueryRows;

public sealed record QueuedStationOrderRow
{
  public required StationOrder StationOrder { get; init; }

  public required Order Order { get; init; }

  public required string StaffMemberName { get; init; }
}
