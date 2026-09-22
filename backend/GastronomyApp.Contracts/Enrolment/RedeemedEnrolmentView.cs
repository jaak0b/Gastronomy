using GastronomyApp.Contracts.Stations;
using GastronomyApp.Contracts.Admin.Staff;

namespace GastronomyApp.Contracts.Enrolment;

public sealed record RedeemedEnrolmentView(Guid DeviceId, string DeviceToken, StaffMemberView? StaffMember, StationSummaryView? Station, string Language);
