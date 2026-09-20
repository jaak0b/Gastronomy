using GastronomyApp.Core.Enums;

namespace GastronomyApp.Api.Contracts;

public sealed record InvitationView(
  Guid InvitationId,
  string QrUrl,
  DateTime ExpiresAtUtc,
  DeviceOwnerKind? OwnerKind,
  StaffMemberView? StaffMember,
  StationSummaryView? Station,
  IReadOnlyList<string> AvailableAddresses);
