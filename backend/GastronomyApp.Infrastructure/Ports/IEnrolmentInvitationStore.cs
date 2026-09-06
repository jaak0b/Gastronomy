using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;

namespace GastronomyApp.Infrastructure.Ports;

public enum EnrolmentRedemptionOutcome
{
  Redeemed,
  CodeInvalid,
  CodeExpired,
  StaffMemberIsOffTheList,
  StationIsOffTheList,
  NoInvitationOutstanding
}

public sealed record EnrolmentInvitationCreated(
  Guid InvitationId,
  string QrCodeValue,
  DateTime ExpiresAtUtc);

public sealed record EnrolmentRedemptionRequest(
  string Code,
  string UserAgent,
  string AcceptLanguageHeader);

public sealed record EnrolmentRedemptionResult(
  EnrolmentRedemptionOutcome Outcome,
  DeviceOwnerKind? OwnerKind,
  Device? Device,
  StaffMember? StaffMember,
  Station? Station,
  string? PlaintextToken,
  Guid? InvitationId);

public interface IEnrolmentInvitationStore
{
  public Task<EnrolmentInvitationCreated> CreateAsync(DeviceOwner owner, CancellationToken cancellationToken);

  public Task<EnrolmentRedemptionResult> RedeemAsync(EnrolmentRedemptionRequest request,
                                                     CancellationToken cancellationToken);
}
