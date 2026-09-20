using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Ports;

public sealed record IssuedDeviceToken(Device Device, string PlaintextToken);
