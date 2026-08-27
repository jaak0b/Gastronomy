using GastronomyApp.Core.Entities;

namespace GastronomyApp.Infrastructure.Ports;

public enum EnrolmentRedemptionOutcome
{
    Redeemed,
    CodeInvalid,
    CodeExpired,
    SixDigitAttemptsExhausted,
}

public sealed record EnrolmentInvitationCreated(
    Guid InvitationId,
    string QrCodeValue,
    string SixDigitCode,
    DateTime ExpiresAtUtc);

public sealed record EnrolmentRedemptionRequest(
    string? Code,
    string? SixDigitCode,
    string Name,
    string UserAgent,
    string AcceptLanguageHeader);

public sealed record EnrolmentRedemptionResult(
    EnrolmentRedemptionOutcome Outcome,
    Device? Device,
    ServerPerson? ServerPerson);

public interface IEnrolmentInvitationStore
{
    public Task<EnrolmentInvitationCreated> CreateAsync(Guid? serverPersonId, CancellationToken cancellationToken);

    public Task<EnrolmentRedemptionResult> RedeemAsync(
        EnrolmentRedemptionRequest request,
        CancellationToken cancellationToken);
}
