namespace GastronomyApp.Api.Auth;

public sealed record StaffDeviceCaller(Guid StaffMemberId, Guid DeviceId, string Language);
