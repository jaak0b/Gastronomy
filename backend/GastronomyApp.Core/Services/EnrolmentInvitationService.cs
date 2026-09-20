using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class EnrolmentInvitationService
{
  private readonly IClock _clock;
  private readonly IDeviceTokenStore _deviceTokenStore;
  private readonly IDeviceOwnerStore _ownerStore;
  private readonly DeviceOwnerRetirement _retirement;
  private readonly IEnrolmentInvitationStore _store;

  public EnrolmentInvitationService(IEnrolmentInvitationStore store,
                                    IDeviceOwnerStore ownerStore,
                                    IDeviceTokenStore deviceTokenStore,
                                    DeviceOwnerRetirement retirement,
                                    IClock clock)
  {
    _store = store;
    _ownerStore = ownerStore;
    _deviceTokenStore = deviceTokenStore;
    _retirement = retirement;
    _clock = clock;
  }

  public async Task<Result<IssuedEnrolmentInvitation, EnrolmentInvitationFailure>> CreateAsync(
    Guid? staffMemberId,
    Guid? stationId,
    CancellationToken cancellationToken)
  {
    if (staffMemberId is not null && stationId is not null)
    {
      return Failed(EnrolmentInvitationFailureReason.AtMostOneOwner);
    }

    DeviceOwner? owner = ReadOwner(staffMemberId, stationId);
    DeviceOwnerRecord? ownerRecord = owner is null
                                       ? null
                                       : await _ownerStore.FindAsync(owner, cancellationToken);

    if (owner is not null && ownerRecord is null)
    {
      return Failed(EnrolmentInvitationFailureReason.OwnerNotFound);
    }

    var deviceToReplace = ownerRecord?.DeviceId;

    EnrolmentInvitationCreated created = await _store.CreateAsync(owner, cancellationToken);
    var revokedDeviceId = await _retirement.RevokeDeviceAsync(deviceToReplace, cancellationToken);

    return Result<IssuedEnrolmentInvitation, EnrolmentInvitationFailure>.Success(new()
                                                                                 {
                                                                                   InvitationId = created.InvitationId,
                                                                                   QRCodeValue = created.QRCodeValue,
                                                                                   ExpiresAtUtc = created.ExpiresAtUtc,
                                                                                   Owner = owner,
                                                                                   OwnerName = ownerRecord?.Name,
                                                                                   RevokedDeviceId = revokedDeviceId
                                                                                 });
  }

  public Task<EnrolmentRedemptionResult> RedeemAsync(EnrolmentRedemptionRequest request,
                                                     CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    return _store.RedeemAsync(request, cancellationToken);
  }

  public async Task<Guid?> RetireHandedOverDeviceAsync(string tokenLookupId,
                                                       string secret,
                                                       Guid newDeviceId,
                                                       CancellationToken cancellationToken)
  {
    DeviceVerificationResult verification =
      await _deviceTokenStore.VerifyAsync(tokenLookupId, secret, cancellationToken);

    if (!verification.IsValid || verification.Device is null || verification.Device.Id == newDeviceId)
    {
      return null;
    }

    return await _retirement.RevokeDeviceAsync(verification.Device.Id, cancellationToken);
  }

  public async Task<Result<Guid, EnrolmentInvitationFailure>> FindRenderableAsync(Guid invitationId,
                                                                                  CancellationToken cancellationToken)
  {
    EnrolmentInvitation? invitation = await _store.FindByIdAsync(invitationId, cancellationToken);

    if (invitation is null)
    {
      return Result<Guid, EnrolmentInvitationFailure>
        .Failed(new() { Reason = EnrolmentInvitationFailureReason.InvitationUnknown });
    }

    if (invitation.ConsumedAtUtc is not null)
    {
      return Result<Guid, EnrolmentInvitationFailure>
        .Failed(new()
                {
                  Reason = invitation.ConsumedByDeviceId is null
                             ? EnrolmentInvitationFailureReason.InvitationReplaced
                             : EnrolmentInvitationFailureReason.InvitationAlreadyUsed
                });
    }

    if (invitation.ExpiresAtUtc <= _clock.UtcNow)
    {
      return Result<Guid, EnrolmentInvitationFailure>
        .Failed(new() { Reason = EnrolmentInvitationFailureReason.InvitationExpired });
    }

    return Result<Guid, EnrolmentInvitationFailure>.Success(invitation.Id);
  }

  private DeviceOwner? ReadOwner(Guid? staffMemberId, Guid? stationId)
  {
    if (staffMemberId is not null)
    {
      return new(DeviceOwnerKind.StaffMember, staffMemberId.Value);
    }

    return stationId is null ? null : new DeviceOwner(DeviceOwnerKind.Station, stationId.Value);
  }

  private Result<IssuedEnrolmentInvitation, EnrolmentInvitationFailure> Failed(
    EnrolmentInvitationFailureReason reason)
  {
    return Result<IssuedEnrolmentInvitation, EnrolmentInvitationFailure>.Failed(new() { Reason = reason });
  }
}
