namespace GastronomyApp.Core.Results;

public sealed record SavedStaffMember(Guid StaffMemberId, string Name, Guid? RevokedDeviceId);
