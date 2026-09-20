namespace GastronomyApp.Core.ReadModels;

public sealed record AdministeredStaffMember(
  Guid StaffMemberId,
  string Name,
  bool IsActive,
  bool HasDevice,
  DateTime? LastSeenAtUtc,
  bool HasOutstandingInvitation);
