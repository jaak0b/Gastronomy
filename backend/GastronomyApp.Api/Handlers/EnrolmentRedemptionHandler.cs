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

    var redemption = await _service.RedeemAsync(request.Code!, request.Name?.Trim(), request.UserAgent ?? string.Empty, httpContext.Request.Headers[AcceptLanguageHeaderName].ToString(), cancellationToken);

    return redemption.Outcome switch
           {
             EnrolmentRedemptionOutcome.Redeemed => await CompletedAsync(redemption, request.PreviousDeviceToken, cancellationToken),
             EnrolmentRedemptionOutcome.CodeInvalid => Refused(redemption, "the code sent does not match the invitation that is outstanding", StatusCodes.Status404NotFound, "EnrolmentCodeUnknown", "enrolment.codeUnknown"),
             EnrolmentRedemptionOutcome.CodeExpired => Refused(redemption, "the outstanding invitation had already expired", StatusCodes.Status410Gone, "EnrolmentCodeNoLongerValid", "enrolment.codeNoLongerValid"),
             EnrolmentRedemptionOutcome.NoInvitationOutstanding => Refused(redemption, "no invitation is outstanding, so the last one was already used or was replaced by a newer one", StatusCodes.Status410Gone, "EnrolmentCodeNoLongerValid", "enrolment.codeNoLongerValid"),
             EnrolmentRedemptionOutcome.StaffMemberIsOffTheList => Refused(redemption, "the person the invitation names is off the list", StatusCodes.Status410Gone, "StaffMemberIsOffTheList", "enrolment.staffMemberIsOffTheList"),
             EnrolmentRedemptionOutcome.StationIsOffTheList => Refused(redemption, "the station the invitation names is switched off", StatusCodes.Status410Gone, "StationIsOffTheList", "enrolment.stationIsOffTheList"),
             EnrolmentRedemptionOutcome.NameRequired => Refused(redemption, "the invitation names nobody and the phone sent no name", StatusCodes.Status400BadRequest, "ValidationFailed", "enrolment.nameMissing"),
             _ => new UnreachableCase().Throw<IResult>(redemption.Outcome)
           };
  }

  private IResult Refused(EnrolmentRedemptionResult redemption, string reason, int statusCode, string code, string messageKey)
  {
    _log.LogWarning("Enrolment refused for invitation {InvitationId}, because {Reason}.", redemption.Invitation?.Id, reason);

    return _resultEnvelope.Problem(statusCode, code, messageKey);
  }

  private async Task<IResult> CompletedAsync(EnrolmentRedemptionResult redemption, string? previousDeviceToken, CancellationToken cancellationToken)
  {
    _invitationCache.ForgetInvitation();

    var owner = redemption.Owner!;
    var device = owner.Device!;

    _log.LogInformation("Enrolment invitation {InvitationId} was redeemed. Device {DeviceId} now belongs to the {DeviceKind} {OwnerId}.", redemption.Invitation?.Id, device.Id, owner.Kind, owner.Id);

    await RetireHandedOverDeviceAsync(previousDeviceToken, device.Id, cancellationToken);

    await _dispatcher.PushEnrolmentCompletedAsync(new(owner.Kind, owner.Id, owner.Name, device.Id), cancellationToken);

    return Results.Ok(new RedeemedEnrolmentView(device.Id, redemption.PlaintextToken!, owner.Kind, BuildStaffMemberView(owner), BuildStationSummaryView(owner), device.Language));
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
