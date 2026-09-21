using ErrorOr;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Results;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Core.Services;

public sealed class EnrolmentInvitationService
{
  private readonly TimeProvider _timeProvider;
  private readonly IAfterCommitActions _afterCommitActions;
  private readonly IEnrolmentCompletionAnnouncer _announcer;
  private readonly IDeviceTokenStore _deviceTokenStore;
  private readonly IOutstandingInvitationCache _invitationCache;
  private readonly ILogger<EnrolmentInvitationService> _logger;
  private readonly IDeviceOwnerStore _ownerStore;
  private readonly DeviceOwnerRetirement _retirement;
  private readonly IEnrolmentInvitationStore _store;
  private readonly IDeviceTokenSplitter _tokenSplitter;

  public EnrolmentInvitationService(IEnrolmentInvitationStore store,
                                    IDeviceOwnerStore ownerStore,
                                    IDeviceTokenStore deviceTokenStore,
                                    IDeviceTokenSplitter tokenSplitter,
                                    IOutstandingInvitationCache invitationCache,
                                    IEnrolmentCompletionAnnouncer announcer,
                                    IAfterCommitActions afterCommitActions,
                                    DeviceOwnerRetirement retirement,
                                    TimeProvider timeProvider,
                                    ILogger<EnrolmentInvitationService> logger)
  {
    _store = store;
    _ownerStore = ownerStore;
    _deviceTokenStore = deviceTokenStore;
    _tokenSplitter = tokenSplitter;
    _invitationCache = invitationCache;
    _announcer = announcer;
    _afterCommitActions = afterCommitActions;
    _retirement = retirement;
    _timeProvider = timeProvider;
    _logger = logger;
  }

  public async Task<ErrorOr<IssuedEnrolmentInvitation>> CreateAsync(Guid? staffMemberId, Guid? stationId, CancellationToken cancellationToken)
  {
    if (staffMemberId is not null && stationId is not null)
      return Refusal.Enrolment.AtMostOneOwner();

    IDeviceOwner? owner = null;

    if (staffMemberId is not null || stationId is not null)
    {
      owner = await FindOwnerAsync(staffMemberId, stationId, cancellationToken);

      if (owner is null)
        return Refusal.Enrolment.OwnerNotFound();
    }

    Guid? deviceToReplace = owner?.DeviceId;

    var issued = await _store.CreateAsync(owner, cancellationToken);
    await _retirement.RevokeDeviceAsync(deviceToReplace, cancellationToken);

    _logger.LogInformation("Enrolment invitation {InvitationId} was created for the {OwnerKind} {OwnerId}, and is valid until {ExpiresAtUtc}. A missing owner means a waiter who types their name when they scan it.",
                           issued.Invitation.Id,
                           issued.Owner?.Kind,
                           issued.Owner?.Id,
                           issued.Invitation.ExpiresAtUtc);

    return issued;
  }

  public async Task<ErrorOr<EnrolmentRedemptionResult>> RedeemAsync(string code, string? name, string userAgent, string acceptLanguageHeader, string? previousDeviceToken, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(code);
    ArgumentNullException.ThrowIfNull(userAgent);
    ArgumentNullException.ThrowIfNull(acceptLanguageHeader);

    ErrorOr<EnrolmentRedemptionResult> redeemed = await _store.RedeemAsync(code, name, userAgent, acceptLanguageHeader, cancellationToken);

    if (redeemed.IsError)
      return redeemed.Errors;

    var redemption = redeemed.Value;
    var owner = redemption.Owner;
    var device = owner.Device!;

    _invitationCache.ForgetInvitation();

    _logger.LogInformation("Enrolment invitation {InvitationId} was redeemed. Device {DeviceId} now belongs to the {DeviceKind} {OwnerId}.", redemption.Invitation.Id, device.Id, owner.Kind, owner.Id);

    var tokenParts = _tokenSplitter.Split(previousDeviceToken);

    if (tokenParts is not null)
    {
      var handedOverOwner = await _deviceTokenStore.VerifyAsync(tokenParts.TokenLookupId, tokenParts.Secret, cancellationToken);
      var handedOverDevice = handedOverOwner?.Device;

      if (handedOverDevice is not null && handedOverDevice.Id != device.Id)
      {
        await _retirement.RevokeDeviceAsync(handedOverDevice.Id, cancellationToken);

        _logger.LogInformation("The browser that was just set up handed over the device {PreviousDeviceId} it still held, so that one is signed out.", handedOverDevice.Id);
      }
    }

    await _afterCommitActions.RunWhenCommittedAsync(announcementCancellationToken => _announcer.AnnounceEnrolmentCompletedAsync(owner, announcementCancellationToken), cancellationToken);

    return redemption;
  }

  public async Task<ErrorOr<string>> ReadOpenQRUrlAsync(Guid invitationId, CancellationToken cancellationToken)
  {
    ErrorOr<EnrolmentInvitation> stillOpen = await EnsureStillOpenAsync(invitationId, cancellationToken);

    if (stillOpen.IsError)
      return stillOpen.Errors;

    var remembered = _invitationCache.Read();

    if (remembered is null || remembered.InvitationId != invitationId)
      return Refusal.Enrolment.QRUnavailable();

    return remembered.QRUrl;
  }

  private async Task<ErrorOr<EnrolmentInvitation>> EnsureStillOpenAsync(Guid invitationId, CancellationToken cancellationToken)
  {
    var invitation = await _store.FindByIdAsync(invitationId, cancellationToken);

    if (invitation is null)
      return Refusal.Enrolment.InvitationUnknown(invitationId);

    if (invitation.ConsumedAtUtc is not null)
    {
      if (invitation.ConsumedByDeviceId is null)
        return Refusal.Enrolment.InvitationReplaced(invitationId);

      return Refusal.Enrolment.InvitationAlreadyUsed(invitationId);
    }

    if (invitation.ExpiresAtUtc <= _timeProvider.GetUtcNow().UtcDateTime)
      return Refusal.Enrolment.InvitationExpired(invitationId);

    return invitation;
  }

  private Task<IDeviceOwner?> FindOwnerAsync(Guid? staffMemberId, Guid? stationId, CancellationToken cancellationToken)
  {
    if (staffMemberId is not null)
      return _ownerStore.FindAsync(DeviceOwnerKind.StaffMember, staffMemberId.Value, cancellationToken);

    return _ownerStore.FindAsync(DeviceOwnerKind.Station, stationId!.Value, cancellationToken);
  }
}
