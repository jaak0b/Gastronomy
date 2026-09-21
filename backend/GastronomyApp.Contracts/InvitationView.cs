using GastronomyApp.Contracts.Enums;

namespace GastronomyApp.Contracts;

public sealed record InvitationView(Guid InvitationId, string QRUrl, DateTime ExpiresAtUtc, DeviceOwnerKind? OwnerKind, StaffMemberView? StaffMember, StationSummaryView? Station, IReadOnlyList<string> AvailableAddresses);
