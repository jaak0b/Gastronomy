using GastronomyApp.Contracts.Enums;

namespace GastronomyApp.Core.Results;

public sealed record DeviceOwner(DeviceOwnerKind Kind, Guid Id);
