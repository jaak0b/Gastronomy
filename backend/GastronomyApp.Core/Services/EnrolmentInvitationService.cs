using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class EnrolmentInvitationService
{
  private readonly IClock _clock;
  private readonly IDeviceTokenStore _deviceTokenStore;
  private readonly IDeviceOwnerStore _ownerStore;
  private readonly DeviceOwnerRetirement _retirement;
  private readonly IEnrolmentInvitationStore _store;

  public EnrolmentInvitationService(IEnrolmentInvitationStore store, IDeviceOwnerStore ownerStore, IDeviceTokenStore deviceTokenStore, DeviceOwnerRetirement retirement, IClock clock)
  {
    _store = store;
    _ownerStore = ownerStore;
    _deviceTokenStore = deviceTokenStore;
    _retirement = retirement;
    _clock = clock;
  }

  public async Task<Result<IssuedEnrolmentInvitation, Failure<EnrolmentInvitationFailureReason>>> CreateAsync(Guid? staffMemberId, Guid? stationId, CancellationToken cancellationToken)
  {
    if (staffMemberId is not null && stationId is not null)
      return Failed(EnrolmentInvitationFailureReason.AtMostOneOwner);

    var owner = ReadOwner(staffMemberId, stationId);
    DeviceOwnerRecord? ownerRecord = null;

    if (owner is not null)
      ownerRecord = await _ownerStore.FindAsync(owner, cancellationToken);

    if (owner is not null && ownerRecord is null)
      return Failed(EnrolmentInvitationFailureReason.OwnerNotFound);

    Guid? deviceToReplace = ownerRecord?.DeviceId;

    var created = await _store.CreateAsync(owner, cancellationToken);
    await _retirement.RevokeDeviceAsync(deviceToReplace, cancellationToken);

    return Result<IssuedEnrolmentInvitation, Failure<EnrolmentInvitationFailureReason>>.Success(new()
                                                                                                {
                                                                                                  InvitationId = created.InvitationId,
                                                                                                  QRCodeValue = created.QRCodeValue,
                                                                                                  ExpiresAtUtc = created.ExpiresAtUtc,
                                                                                                  Owner = owner,
                                                                                                  OwnerName = ownerRecord?.Name
                                                                                                });
  }

  public Task<EnrolmentRedemptionResult> RedeemAsync(string code, string? name, string userAgent, string acceptLanguageHeader, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(code);
    ArgumentNullException.ThrowIfNull(userAgent);
    ArgumentNullException.ThrowIfNull(acceptLanguageHeader);

    return _store.RedeemAsync(code, name, userAgent, acceptLanguageHeader, cancellationToken);
  }

  public async Task<Guid?> RetireHandedOverDeviceAsync(string tokenLookupId, string secret, Guid newDeviceId, CancellationToken cancellationToken)
  {
    var verification = await _deviceTokenStore.VerifyAsync(tokenLookupId, secret, cancellationToken);

    if (!verification.IsValid || verification.Device is null || verification.Device.Id == newDeviceId)
      return null;

    return await _retirement.RevokeDeviceAsync(verification.Device.Id, cancellationToken);
  }

  public async Task<Result<OpenEnrolmentInvitation, Failure<EnrolmentInvitationFailureReason>>> EnsureStillOpenAsync(Guid invitationId, CancellationToken cancellationToken)
  {
    var invitation = await _store.FindByIdAsync(invitationId, cancellationToken);

    if (invitation is null)
      return Refused(EnrolmentInvitationFailureReason.InvitationUnknown);

    if (invitation.ConsumedAtUtc is not null)
    {
      if (invitation.ConsumedByDeviceId is null)
        return Refused(EnrolmentInvitationFailureReason.InvitationReplaced);

      return Refused(EnrolmentInvitationFailureReason.InvitationAlreadyUsed);
    }

    if (invitation.ExpiresAtUtc <= _clock.UtcNow)
      return Refused(EnrolmentInvitationFailureReason.InvitationExpired);

    return Result<OpenEnrolmentInvitation, Failure<EnrolmentInvitationFailureReason>>.Success(new(invitation.Id, invitation.ExpiresAtUtc));
  }

  private DeviceOwner? ReadOwner(Guid? staffMemberId, Guid? stationId)
  {
    if (staffMemberId is not null)
      return new(DeviceOwnerKind.StaffMember, staffMemberId.Value);

    if (stationId is null)
      return null;

    return new(DeviceOwnerKind.Station, stationId.Value);
  }

  private Result<IssuedEnrolmentInvitation, Failure<EnrolmentInvitationFailureReason>> Failed(EnrolmentInvitationFailureReason reason)
  {
    return Result<IssuedEnrolmentInvitation, Failure<EnrolmentInvitationFailureReason>>.Failed(new() { Reason = reason });
  }

  private Result<OpenEnrolmentInvitation, Failure<EnrolmentInvitationFailureReason>> Refused(EnrolmentInvitationFailureReason reason)
  {
    return Result<OpenEnrolmentInvitation, Failure<EnrolmentInvitationFailureReason>>.Failed(new() { Reason = reason });
  }
}
