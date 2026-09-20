namespace GastronomyApp.Api.Auth.Callers;

public sealed record StationDeviceCaller(Guid StationId, Guid DeviceId, string Language);
