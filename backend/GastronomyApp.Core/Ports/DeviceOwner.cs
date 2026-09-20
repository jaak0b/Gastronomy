using GastronomyApp.Core.Enums;

namespace GastronomyApp.Core.Ports;

public sealed record DeviceOwner(DeviceOwnerKind Kind, Guid Id);
