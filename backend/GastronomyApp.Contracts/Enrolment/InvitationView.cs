using GastronomyApp.Contracts.Admin.Staff;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Contracts.Stations;

namespace GastronomyApp.Contracts.Enrolment;

public sealed record InvitationView
{
  public required Guid InvitationId { get; init; }

  public required string QRUrl { get; init; }

  public required DateTime ExpiresAtUtc { get; init; }

  public required DeviceOwnerKind? OwnerKind { get; init; }

  public required StaffMemberView? StaffMember { get; init; }

  public required StationSummaryView? Station { get; init; }

  public required IReadOnlyList<string> AvailableAddresses { get; init; }
}
