namespace GastronomyApp.Api.Contracts;

public sealed record AdminStaffMemberListView(IReadOnlyList<AdminStaffMemberView> StaffMembers);
