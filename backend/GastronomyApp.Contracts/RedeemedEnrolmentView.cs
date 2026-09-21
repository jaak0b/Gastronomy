using GastronomyApp.Contracts.Enums;

namespace GastronomyApp.Contracts;

public sealed record RedeemedEnrolmentView(Guid DeviceId, string DeviceToken, DeviceOwnerKind DeviceKind, StaffMemberView? StaffMember, StationSummaryView? Station, string Language);
