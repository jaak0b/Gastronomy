using GastronomyApp.Core.Enums;

namespace GastronomyApp.Api.Auth.Callers;

public sealed record DeviceCaller(DeviceOwnerKind OwnerKind, Guid OwnerId, Guid DeviceId, string Language);
