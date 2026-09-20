using GastronomyApp.Core.Entities;

namespace GastronomyApp.Infrastructure.Ports;

public sealed record DeviceVerificationResult(bool IsValid, Device? Device, DeviceOwner? Owner);
