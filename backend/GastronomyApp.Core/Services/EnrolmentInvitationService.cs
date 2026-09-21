using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class EnrolmentInvitationService
{
  private readonly TimeProvider _timeProvider;
  private readonly IDeviceTokenStore _deviceTokenStore;
  private readonly IDeviceOwnerStore _ownerStore;
  private readonly DeviceOwnerRetirement _retirement;
  private readonly IEnrolmentInvitationStore _store;

  public EnrolmentInvitationService(IEnrolmentInvitationStore store, IDeviceOwnerStore ownerStore, IDeviceTokenStore deviceTokenStore, DeviceOwnerRetirement retirement, TimeProvider timeProvider)
  {
    _store = store;
    _ownerStore = ownerStore;
    _deviceTokenStore = deviceTokenStore;
    _retirement = retirement;
    _timeProvider = timeProvider;
  }

  public async Task<Result<IssuedEnrolmentInvitation, Failure<EnrolmentInvitationFailureReason>>> CreateAsync(Guid? staffMemberId, Guid? stationId, CancellationToken cancellationToken)
  {
    if (staffMemberId is not null && stationId is not null)
      return Failed(EnrolmentInvitationFailureReason.AtMostOneOwner);

    IDeviceOwner? owner = null;

    if (staffMemberId is not null || stationId is not null)
    {
      owner = await FindOwnerAsync(staffMemberId, stationId, cancellationToken);

      if (owner is null)
        return Failed(EnrolmentInvitationFailureReason.OwnerNotFound);
    }

    Guid? deviceToReplace = owner?.DeviceId;

    var issued = await _store.CreateAsync(owner, cancellationToken);
    await _retirement.RevokeDeviceAsync(deviceToReplace, cancellationToken);

    return Result<IssuedEnrolmentInvitation, Failure<EnrolmentInvitationFailureReason>>.Success(issued);
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
    var owner = await _deviceTokenStore.VerifyAsync(tokenLookupId, secret, cancellationToken);

    if (owner?.Device is null || owner.Device.Id == newDeviceId)
      return null;

    return await _retirement.RevokeDeviceAsync(owner.Device.Id, cancellationToken);
  }

  public async Task<Result<EnrolmentInvitation, Failure<EnrolmentInvitationFailureReason>>> EnsureStillOpenAsync(Guid invitationId, CancellationToken cancellationToken)
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

    if (invitation.ExpiresAtUtc <= _timeProvider.GetUtcNow().UtcDateTime)
      return Refused(EnrolmentInvitationFailureReason.InvitationExpired);

    return Result<EnrolmentInvitation, Failure<EnrolmentInvitationFailureReason>>.Success(invitation);
  }

  private Task<IDeviceOwner?> FindOwnerAsync(Guid? staffMemberId, Guid? stationId, CancellationToken cancellationToken)
  {
    if (staffMemberId is not null)
      return _ownerStore.FindAsync(DeviceOwnerKind.StaffMember, staffMemberId.Value, cancellationToken);

    return _ownerStore.FindAsync(DeviceOwnerKind.Station, stationId!.Value, cancellationToken);
  }

  private Result<IssuedEnrolmentInvitation, Failure<EnrolmentInvitationFailureReason>> Failed(EnrolmentInvitationFailureReason reason)
  {
    return Result<IssuedEnrolmentInvitation, Failure<EnrolmentInvitationFailureReason>>.Failed(new() { Reason = reason });
  }

  private Result<EnrolmentInvitation, Failure<EnrolmentInvitationFailureReason>> Refused(EnrolmentInvitationFailureReason reason)
  {
    return Result<EnrolmentInvitation, Failure<EnrolmentInvitationFailureReason>>.Failed(new() { Reason = reason });
  }
}
