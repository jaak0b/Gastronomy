using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Ports;

public sealed record DeviceVerificationResult(bool IsValid, Device? Device, DeviceOwner? Owner);
