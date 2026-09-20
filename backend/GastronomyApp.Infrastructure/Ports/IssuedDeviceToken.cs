using GastronomyApp.Core.Entities;

namespace GastronomyApp.Infrastructure.Ports;

public sealed record IssuedDeviceToken(Device Device, string PlaintextToken);
