using GastronomyApp.Core.Entities;

namespace GastronomyApp.Infrastructure.QueryRows;

public sealed record AdministeredStaffMemberRow
{
  public required StaffMember StaffMember { get; init; }

  public required DateTime? LastSeenAtUtc { get; init; }

  public required bool HasOutstandingInvitation { get; init; }
}
