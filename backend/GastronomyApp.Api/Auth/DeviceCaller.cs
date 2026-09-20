using GastronomyApp.Core.Enums;

namespace GastronomyApp.Api.Auth;

public sealed record DeviceCaller(DeviceOwnerKind OwnerKind, Guid OwnerId, Guid DeviceId, string Language);
