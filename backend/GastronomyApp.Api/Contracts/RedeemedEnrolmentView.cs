using GastronomyApp.Core.Enums;

namespace GastronomyApp.Api.Contracts;

public sealed record RedeemedEnrolmentView(
  Guid DeviceId,
  string DeviceToken,
  DeviceOwnerKind DeviceKind,
  StaffMemberView? StaffMember,
  StationSummaryView? Station,
  string Language);
