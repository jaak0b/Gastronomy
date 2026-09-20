namespace GastronomyApp.Api.Values;

public sealed record StaffDeviceCaller(Guid StaffMemberId, Guid DeviceId, string Language);
