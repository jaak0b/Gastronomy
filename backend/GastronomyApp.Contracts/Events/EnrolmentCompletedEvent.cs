using GastronomyApp.Contracts.Enums;

namespace GastronomyApp.Contracts.Events;

public sealed record EnrolmentCompletedEvent(DeviceOwnerKind DeviceKind, Guid OwnerId, string OwnerName, Guid DeviceId);
