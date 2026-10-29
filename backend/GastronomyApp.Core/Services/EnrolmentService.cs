using System.Security.Cryptography;
using ErrorOr;
using GastronomyApp.Contracts.Enrolment;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Results;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Core.Services;

public sealed class EnrolmentService
{
  private const int QRCodeLengthBytes = 32;
  private const int SecretLengthBytes = 32;
  private const int InvitationLifetimeMinutes = 5;
  private const string GermanLanguage = "de";
  private const string EnglishLanguage = "en";

  private readonly IAfterCommitActions _afterCommitActions;
  private readonly IEnrolmentCompletionAnnouncer _announcer;
  private readonly IGastronomyDatabase _database;
  private readonly IOutstandingInvitationCache _invitationCache;
  private readonly ILogger<EnrolmentService> _logger;
  private readonly DeviceOwnerLookup _lookup;
  private readonly TypeAdapterConfig _mapping;
  private readonly DeviceOwnerRetirement _retirement;
  private readonly ISecretHasher _secretHasher;
  private readonly TimeProvider _timeProvider;
  private readonly IDeviceTokenSplitter _tokenSplitter;
  private readonly IEnrolmentUrlBuilder _urlBuilder;

  public EnrolmentService(IGastronomyDatabase database,
                          TypeAdapterConfig mapping,
                          DeviceOwnerLookup lookup,
                          DeviceOwnerRetirement retirement,
                          ISecretHasher secretHasher,
                          IDeviceTokenSplitter tokenSplitter,
                          IOutstandingInvitationCache invitationCache,
                          IEnrolmentUrlBuilder urlBuilder,
                          IEnrolmentCompletionAnnouncer announcer,
                          IAfterCommitActions afterCommitActions,
                          TimeProvider timeProvider,
                          ILogger<EnrolmentService> logger)
  {
    _database = database;
    _mapping = mapping;
    _lookup = lookup;
    _retirement = retirement;
    _secretHasher = secretHasher;
    _tokenSplitter = tokenSplitter;
    _invitationCache = invitationCache;
    _urlBuilder = urlBuilder;
    _announcer = announcer;
    _afterCommitActions = afterCommitActions;
    _timeProvider = timeProvider;
    _logger = logger;
  }

  public async Task<ErrorOr<InvitationView>> CreateInvitationAsync(Guid? staffMemberId, Guid? stationId, CancellationToken cancellationToken)
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

    var issued = await IssueInvitationAsync(owner, cancellationToken);
    await _retirement.RevokeDeviceAsync(deviceToReplace, cancellationToken);

    _invitationCache.Remember(new(issued.Invitation.Id, issued.QRCodeValue, issued.QRUrl, issued.Invitation.ExpiresAtUtc));

    _logger.LogInformation("Enrolment invitation {InvitationId} was created for the owner {OwnerId}, and is valid until {ExpiresAtUtc}. A missing owner means a waiter who types their name when they scan it.", issued.Invitation.Id, issued.Owner?.Id, issued.Invitation.ExpiresAtUtc);

    return issued.Adapt<InvitationView>(_mapping);
  }

  public async Task<ErrorOr<RedeemedEnrolmentView>> RedeemAsync(RedeemEnrolmentRequest request, string acceptLanguageHeader, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(request.Code);
    ArgumentNullException.ThrowIfNull(acceptLanguageHeader);

    var now = _timeProvider.GetUtcNow().UtcDateTime;
    ErrorOr<EnrolmentInvitation> openInvitation = await FindRedeemableInvitationAsync(request.Code, now, cancellationToken);

    if (openInvitation.IsError)
      return openInvitation.Errors;

    var invitation = openInvitation.Value;
    ErrorOr<IDeviceOwner> redeeming = await FindRedeemingOwnerAsync(invitation.Id, request.Name, now, cancellationToken);

    if (redeeming.IsError)
      return redeeming.Errors;

    var owner = redeeming.Value;
    var issued = await IssueDeviceAsync(owner, ResolveLanguage(acceptLanguageHeader), now, cancellationToken);

    invitation.ConsumedAtUtc = now;
    invitation.ConsumedByDeviceId = issued.Id;
    owner.EnrolmentInvitationId = null;
    owner.EnrolmentInvitation = null;
    await _database.SaveChangesAsync(cancellationToken);

    _invitationCache.ForgetInvitation();

    _logger.LogInformation("Enrolment invitation {InvitationId} was redeemed. Device {DeviceId} now belongs to the owner {OwnerId}.", invitation.Id, issued.Id, owner.Id);

    await RevokeTheDeviceTheBrowserStillHeldAsync(request.PreviousDeviceToken, issued.Id, cancellationToken);
    await _afterCommitActions.RunWhenCommittedAsync(announcementCancellationToken => _announcer.AnnounceEnrolmentCompletedAsync(owner, announcementCancellationToken), cancellationToken);

    return new EnrolmentRedemptionResult(owner, issued.PlaintextToken).Adapt<RedeemedEnrolmentView>(_mapping);
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

  private async Task<IssuedEnrolmentInvitation> IssueInvitationAsync(IDeviceOwner? owner, CancellationToken cancellationToken)
  {
    var now = _timeProvider.GetUtcNow().UtcDateTime;

    await ConsumeEveryUnconsumedPredecessorIncludingExpiredOnesAsync(now, cancellationToken);

    var qrCodeValue = Convert.ToHexString(RandomNumberGenerator.GetBytes(QRCodeLengthBytes));
    var hashedQRCode = _secretHasher.Hash(qrCodeValue);

    EnrolmentInvitation invitation = new()
                                     {
                                       Id = Guid.NewGuid(),
                                       QRCodeHash = hashedQRCode.Hash,
                                       QRCodeSalt = hashedQRCode.Salt,
                                       QRCodeAlgorithm = hashedQRCode.Algorithm,
                                       CreatedAtUtc = now,
                                       ExpiresAtUtc = now.AddMinutes(InvitationLifetimeMinutes),
                                       ConsumedAtUtc = null,
                                       ConsumedByDeviceId = null
                                     };

    _database.EnrolmentInvitations.Add(invitation);

    if (owner is not null)
    {
      owner.EnrolmentInvitationId = invitation.Id;
      owner.EnrolmentInvitation = invitation;
    }

    await _database.SaveChangesAsync(cancellationToken);

    return new(invitation, qrCodeValue, _urlBuilder.BuildEnrolmentUrl(qrCodeValue), owner);
  }

  private async Task ConsumeEveryUnconsumedPredecessorIncludingExpiredOnesAsync(DateTime now, CancellationToken cancellationToken)
  {
    List<EnrolmentInvitation> unconsumedPredecessors = await _database.EnrolmentInvitations.Where(invitation => invitation.ConsumedAtUtc == null).ToListAsync(cancellationToken);

    if (unconsumedPredecessors.Count == 0)
      return;

    foreach (var predecessor in unconsumedPredecessors)
      predecessor.ConsumedAtUtc = now;

    await ForgetInvitationsAsync(unconsumedPredecessors.Select(predecessor => predecessor.Id).ToList(), cancellationToken);
    await _database.SaveChangesAsync(cancellationToken);
  }

  private async Task ForgetInvitationsAsync(List<Guid> invitationIds, CancellationToken cancellationToken)
  {
    List<StaffMember> staffMembers = await _database.StaffMembers.Where(staffMember => staffMember.EnrolmentInvitationId != null && invitationIds.Contains(staffMember.EnrolmentInvitationId.Value)).ToListAsync(cancellationToken);
    List<Station> stations = await _database.Stations.Where(station => station.EnrolmentInvitationId != null && invitationIds.Contains(station.EnrolmentInvitationId.Value)).ToListAsync(cancellationToken);

    foreach (IDeviceOwner owner in staffMembers.Concat<IDeviceOwner>(stations))
    {
      owner.EnrolmentInvitationId = null;
      owner.EnrolmentInvitation = null;
    }
  }

  private async Task<ErrorOr<EnrolmentInvitation>> FindRedeemableInvitationAsync(string code, DateTime now, CancellationToken cancellationToken)
  {
    var invitation = await _database.EnrolmentInvitations.FirstOrDefaultAsync(candidate => candidate.ConsumedAtUtc == null, cancellationToken);

    if (invitation is null)
      return Refusal.EnrolmentRedemption.NoInvitationOutstanding();

    if (invitation.ExpiresAtUtc <= now)
      return Refusal.EnrolmentRedemption.CodeExpired(invitation.Id);

    var qrCodeMatches = _secretHasher.Verify(code, invitation.QRCodeHash, invitation.QRCodeSalt, invitation.QRCodeIterations, invitation.QRCodeAlgorithm);

    if (!qrCodeMatches)
      return Refusal.EnrolmentRedemption.CodeInvalid(invitation.Id);

    return invitation;
  }

  private async Task<ErrorOr<IDeviceOwner>> FindRedeemingOwnerAsync(Guid invitationId, string? typedName, DateTime now, CancellationToken cancellationToken)
  {
    var owner = await _lookup.FindByInvitationAsync(invitationId, cancellationToken);

    if (owner is null)
    {
      if (string.IsNullOrWhiteSpace(typedName))
        return Refusal.EnrolmentRedemption.NameRequired(invitationId);

      StaffMember newStaffMember = new()
                                   {
                                     Id = Guid.NewGuid(),
                                     Name = typedName,
                                     IsActive = true,
                                     CreatedAtUtc = now
                                   };

      _database.StaffMembers.Add(newStaffMember);

      return newStaffMember;
    }

    if (owner.IsActive)
      return owner.ToErrorOr();

    return OffTheListRefusalFor(owner, invitationId);
  }

  private Error OffTheListRefusalFor(IDeviceOwner owner, Guid invitationId)
  {
    return owner switch
           {
             StaffMember => Refusal.EnrolmentRedemption.StaffMemberIsOffTheList(invitationId),
             Station => Refusal.EnrolmentRedemption.StationIsOffTheList(invitationId),
             _ => throw new InvalidOperationException($"Unhandled value: {owner}")
           };
  }

  private async Task<IssuedDevice> IssueDeviceAsync(IDeviceOwner owner, string language, DateTime now, CancellationToken cancellationToken)
  {
    await RetireCurrentDeviceAsync(owner, cancellationToken);

    var tokenLookupId = Guid.NewGuid().ToString("N");
    var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(SecretLengthBytes));
    var hashedSecret = _secretHasher.Hash(secret);

    Device device = new()
                    {
                      Id = Guid.NewGuid(),
                      Language = language,
                      TokenHash = hashedSecret.Hash,
                      TokenSalt = hashedSecret.Salt,
                      TokenAlgorithm = hashedSecret.Algorithm,
                      TokenLookupId = tokenLookupId,
                      CreatedAtUtc = now
                    };

    _database.Devices.Add(device);
    owner.DeviceId = device.Id;
    owner.Device = device;

    return new(device.Id, $"{tokenLookupId}.{secret}");
  }

  private async Task RetireCurrentDeviceAsync(IDeviceOwner owner, CancellationToken cancellationToken)
  {
    if (owner.DeviceId is null)
      return;

    var previousDeviceId = owner.DeviceId.Value;
    var previousDevice = await _database.Devices.FirstOrDefaultAsync(candidate => candidate.Id == previousDeviceId, cancellationToken);

    owner.DeviceId = null;
    owner.Device = null;

    if (previousDevice is not null)
      _database.Devices.Remove(previousDevice);
  }

  private async Task RevokeTheDeviceTheBrowserStillHeldAsync(string? previousDeviceToken, Guid newDeviceId, CancellationToken cancellationToken)
  {
    var tokenParts = _tokenSplitter.Split(previousDeviceToken);

    if (tokenParts is null)
      return;

    var handedOverOwner = await _lookup.FindByTokenAsync(tokenParts.TokenLookupId, tokenParts.Secret, cancellationToken);
    var handedOverDevice = handedOverOwner?.Device;

    if (handedOverDevice is null || handedOverDevice.Id == newDeviceId)
      return;

    await _retirement.RevokeDeviceAsync(handedOverDevice.Id, cancellationToken);

    _logger.LogInformation("The browser that was just set up handed over the device {PreviousDeviceId} it still held, so that one is signed out.", handedOverDevice.Id);
  }

  private async Task<ErrorOr<EnrolmentInvitation>> EnsureStillOpenAsync(Guid invitationId, CancellationToken cancellationToken)
  {
    var invitation = await _database.EnrolmentInvitations.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == invitationId, cancellationToken);

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

  private async Task<IDeviceOwner?> FindOwnerAsync(Guid? staffMemberId, Guid? stationId, CancellationToken cancellationToken)
  {
    if (staffMemberId is not null)
      return await _lookup.FindStaffMemberAsync(staffMemberId.Value, cancellationToken);

    return await _lookup.FindStationAsync(stationId!.Value, cancellationToken);
  }

  private string ResolveLanguage(string acceptLanguageHeader)
  {
    var firstTag = acceptLanguageHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault(string.Empty);

    if (firstTag.StartsWith(GermanLanguage, StringComparison.OrdinalIgnoreCase))
      return GermanLanguage;

    return EnglishLanguage;
  }

  private sealed record IssuedDevice(Guid Id, string PlaintextToken);
}
