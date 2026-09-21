using GastronomyApp.Contracts.Stations;
using GastronomyApp.Contracts.Admin.Staff;
using GastronomyApp.Contracts.Enums;

namespace GastronomyApp.Contracts.Enrolment;

public sealed record RedeemedEnrolmentView(Guid DeviceId, string DeviceToken, DeviceOwnerKind DeviceKind, StaffMemberView? StaffMember, StationSummaryView? Station, string Language);
