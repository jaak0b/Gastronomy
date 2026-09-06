using GastronomyApp.Core.Enums;

namespace GastronomyApp.Api.Contracts;

public sealed record RedeemEnrolmentRequest
{
  public string? Code { get; init; }

  public string? Name { get; init; }

  public string? UserAgent { get; init; }
}

public sealed record RedeemedEnrolmentView(
  Guid DeviceId,
  string DeviceToken,
  DeviceOwnerKind DeviceKind,
  StaffMemberView? StaffMember,
  StationSummaryView? Station,
  string Language);
