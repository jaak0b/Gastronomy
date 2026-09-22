using GastronomyApp.Contracts.Admin.Staff;
using GastronomyApp.Contracts.Stations;

namespace GastronomyApp.Contracts.Session;

public sealed record SessionView
{
  public required Guid DeviceId { get; init; }

  public required StaffMemberView? StaffMember { get; init; }

  public required StationSummaryView? Station { get; init; }

  public required string Language { get; init; }
}
