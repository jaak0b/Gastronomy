using GastronomyApp.Core.Enums;

namespace GastronomyApp.Api.Values;

public sealed record DeviceCaller(DeviceOwnerKind OwnerKind, Guid OwnerId, Guid DeviceId, string Language);
