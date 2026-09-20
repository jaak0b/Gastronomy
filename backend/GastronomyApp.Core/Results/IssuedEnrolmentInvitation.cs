using GastronomyApp.Core.Ports;

namespace GastronomyApp.Core.Results;

public sealed record IssuedEnrolmentInvitation
{
  public required Guid InvitationId { get; init; }

  public required string QRCodeValue { get; init; }

  public required DateTime ExpiresAtUtc { get; init; }

  public required DeviceOwner? Owner { get; init; }

  public required string? OwnerName { get; init; }

  public required Guid? RevokedDeviceId { get; init; }
}
