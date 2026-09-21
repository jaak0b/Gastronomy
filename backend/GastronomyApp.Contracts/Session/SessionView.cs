using GastronomyApp.Contracts.Stations;
using GastronomyApp.Contracts.Admin.Staff;
using GastronomyApp.Contracts.Enums;

namespace GastronomyApp.Contracts.Session;

public sealed record SessionView(Guid DeviceId, DeviceOwnerKind DeviceKind, StaffMemberView? StaffMember, StationSummaryView? Station, string Language);
