namespace GastronomyApp.Contracts.Admin.Staff;

public sealed record AdminStaffMemberListView(IReadOnlyList<AdminStaffMemberView> StaffMembers);
