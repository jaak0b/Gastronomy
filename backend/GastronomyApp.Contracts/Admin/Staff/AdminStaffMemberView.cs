namespace GastronomyApp.Contracts.Admin.Staff;

public sealed record AdminStaffMemberView(Guid StaffMemberId, string Name, bool IsActive, bool HasDevice, DateTime? LastSeenAtUtc, bool HasOutstandingInvitation);
