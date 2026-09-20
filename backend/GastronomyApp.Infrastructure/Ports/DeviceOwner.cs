using GastronomyApp.Core.Enums;

namespace GastronomyApp.Infrastructure.Ports;

public sealed record DeviceOwner(DeviceOwnerKind Kind, Guid Id);
