using GastronomyApp.Contracts.Enums;

namespace GastronomyApp.Contracts;

public sealed record SessionView(Guid DeviceId, DeviceOwnerKind DeviceKind, StaffMemberView? StaffMember, StationSummaryView? Station, string Language);
