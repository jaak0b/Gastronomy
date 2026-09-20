namespace GastronomyApp.Api.Contracts;

public sealed record AdminStaffMemberView(
  Guid StaffMemberId,
  string Name,
  bool IsActive,
  bool HasDevice,
  DateTime? LastSeenAtUtc,
  bool HasOutstandingInvitation);
