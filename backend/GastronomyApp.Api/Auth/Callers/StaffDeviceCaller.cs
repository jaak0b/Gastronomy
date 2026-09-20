namespace GastronomyApp.Api.Auth.Callers;

public sealed record StaffDeviceCaller(Guid StaffMemberId, Guid DeviceId, string Language);
