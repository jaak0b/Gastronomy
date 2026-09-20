using GastronomyApp.Core.Enums;

namespace GastronomyApp.Api.Contracts;

public sealed record SessionView(Guid DeviceId, DeviceOwnerKind DeviceKind, StaffMemberView? StaffMember, StationSummaryView? Station, string Language);
