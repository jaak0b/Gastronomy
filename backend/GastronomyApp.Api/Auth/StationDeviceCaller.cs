namespace GastronomyApp.Api.Auth;

public sealed record StationDeviceCaller(Guid StationId, Guid DeviceId, string Language);
