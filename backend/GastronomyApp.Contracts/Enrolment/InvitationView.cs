using GastronomyApp.Contracts.Stations;
using GastronomyApp.Contracts.Admin.Staff;
using GastronomyApp.Contracts.Enums;

namespace GastronomyApp.Contracts.Enrolment;

public sealed record InvitationView(Guid InvitationId, string QRUrl, DateTime ExpiresAtUtc, DeviceOwnerKind? OwnerKind, StaffMemberView? StaffMember, StationSummaryView? Station, IReadOnlyList<string> AvailableAddresses);
