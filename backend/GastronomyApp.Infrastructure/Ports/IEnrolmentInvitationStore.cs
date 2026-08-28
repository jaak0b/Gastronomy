using GastronomyApp.Core.Entities;

namespace GastronomyApp.Infrastructure.Ports;

public enum EnrolmentRedemptionOutcome
{
  Redeemed,
  CodeInvalid,
  CodeExpired,
  StaffMemberIsOffTheList,
  NameRequired,
}

public sealed record EnrolmentInvitationCreated(
    Guid InvitationId,
    string QrCodeValue,
    DateTime ExpiresAtUtc);

public sealed record EnrolmentRedemptionRequest(
    string Code,
    string? Name,
    string UserAgent,
    string AcceptLanguageHeader);

public sealed record EnrolmentRedemptionResult(
    EnrolmentRedemptionOutcome Outcome,
    Device? Device,
    StaffMember? StaffMember,
    string? PlaintextToken);

public interface IEnrolmentInvitationStore
{
  public Task<EnrolmentInvitationCreated> CreateAsync(Guid? staffMemberId, CancellationToken cancellationToken);

  public Task<EnrolmentRedemptionResult> RedeemAsync(
      EnrolmentRedemptionRequest request,
      CancellationToken cancellationToken);
}
