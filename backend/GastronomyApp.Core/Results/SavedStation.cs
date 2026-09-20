namespace GastronomyApp.Core.Results;

public sealed record SavedStation(Guid StationId, bool SomethingChanged, Guid? RevokedDeviceId);
