using GastronomyApp.Core.Enums;

namespace GastronomyApp.Api.Contracts;

public sealed record EnrolmentCompletedEvent(
  DeviceOwnerKind DeviceKind,
  Guid OwnerId,
  string OwnerName,
  Guid DeviceId);
