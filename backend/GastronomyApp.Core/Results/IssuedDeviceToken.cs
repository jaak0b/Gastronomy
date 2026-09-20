using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Results;

public sealed record IssuedDeviceToken(Device Device, string PlaintextToken);
