using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Results;

public sealed record DeviceVerificationResult(bool IsValid, Device? Device, DeviceOwner? Owner);
