using ErrorOr;
using GastronomyApp.Api.Auth;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Hosting;
using GastronomyApp.Api.Hub;
using GastronomyApp.Contracts.Admin.Staff;
using GastronomyApp.Contracts.Enrolment;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Contracts.Stations;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Handlers;

public sealed class EnrolmentRedemptionHandler
{
  private const string AcceptLanguageHeaderName = "Accept-Language";

  private readonly HubNotificationDispatcher _dispatcher;
  private readonly OutstandingInvitationCache _invitationCache;
  private readonly ILogger<EnrolmentRedemptionHandler> _log;
  private readonly ResultEnvelope _resultEnvelope;
  private readonly EnrolmentInvitationService _service;
  private readonly DeviceTokenSplitter _tokenSplitter;

  public EnrolmentRedemptionHandler(EnrolmentInvitationService service, HubNotificationDispatcher dispatcher, OutstandingInvitationCache invitationCache, ResultEnvelope resultEnvelope, DeviceTokenSplitter tokenSplitter, ILogger<EnrolmentRedemptionHandler> log)
  {
    _service = service;
    _dispatcher = dispatcher;
    _invitationCache = invitationCache;
    _resultEnvelope = resultEnvelope;
    _tokenSplitter = tokenSplitter;
    _log = log;
  }

  public async Task<IResult> RedeemAsync(RedeemEnrolmentRequest request, HttpContext httpContext, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(httpContext);

    return await _service.RedeemAsync(request.Code!, request.Name?.Trim(), request.UserAgent ?? string.Empty, httpContext.Request.Headers[AcceptLanguageHeaderName].ToString(), cancellationToken)
                         .MatchAsync(redemption => CompletedAsync(redemption, request.PreviousDeviceToken, cancellationToken), refusedLines => Task.FromResult(_resultEnvelope.Refuse(refusedLines)));
  }


  private async Task<IResult> CompletedAsync(EnrolmentRedemptionResult redemption, string? previousDeviceToken, CancellationToken cancellationToken)
  {
    _invitationCache.ForgetInvitation();

    var owner = redemption.Owner;
    var device = owner.Device!;

    _log.LogInformation("Enrolment invitation {InvitationId} was redeemed. Device {DeviceId} now belongs to the {DeviceKind} {OwnerId}.", redemption.Invitation.Id, device.Id, owner.Kind, owner.Id);

    await RetireHandedOverDeviceAsync(previousDeviceToken, device.Id, cancellationToken);

    await _dispatcher.PushEnrolmentCompletedAsync(new(owner.Kind, owner.Id, owner.Name, device.Id), cancellationToken);

    return Results.Ok(new RedeemedEnrolmentView(device.Id, redemption.PlaintextToken, owner.Kind, BuildStaffMemberView(owner), BuildStationSummaryView(owner), device.Language));
  }

  private StaffMemberView? BuildStaffMemberView(IDeviceOwner owner)
  {
    if (owner.Kind != DeviceOwnerKind.StaffMember)
      return null;

    return new(owner.Id, owner.Name);
  }

  private StationSummaryView? BuildStationSummaryView(IDeviceOwner owner)
  {
    if (owner.Kind != DeviceOwnerKind.Station)
      return null;

    return new(owner.Id, owner.Name);
  }

  private async Task RetireHandedOverDeviceAsync(string? previousDeviceToken, Guid newDeviceId, CancellationToken cancellationToken)
  {
    var tokenParts = _tokenSplitter.Split(previousDeviceToken);

    if (tokenParts is null)
      return;

    Guid? retiredDeviceId = await _service.RetireHandedOverDeviceAsync(tokenParts.TokenLookupId, tokenParts.Secret, newDeviceId, cancellationToken);

    if (retiredDeviceId is null)
      return;

    _log.LogInformation("The browser that was just set up handed over the device {PreviousDeviceId} it still held, so that one is signed out.", retiredDeviceId);
  }
}
