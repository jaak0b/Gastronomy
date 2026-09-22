namespace GastronomyApp.Contracts.Events;

public sealed record EnrolmentCompletedEvent(Guid? StaffMemberId, Guid? StationId, string OwnerName, Guid DeviceId);
