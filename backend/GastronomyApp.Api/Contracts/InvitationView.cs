using GastronomyApp.Core.Enums;

namespace GastronomyApp.Api.Contracts;

public sealed record InvitationView(
  Guid InvitationId,
  string QRUrl,
  DateTime ExpiresAtUtc,
  DeviceOwnerKind? OwnerKind,
  StaffMemberView? StaffMember,
  StationSummaryView? Station,
  IReadOnlyList<string> AvailableAddresses);
