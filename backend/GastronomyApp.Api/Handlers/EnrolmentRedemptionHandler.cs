using GastronomyApp.Api.Announcers;
using GastronomyApp.Api.Auth;
using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Hosting;
using GastronomyApp.Api.Hub;
using GastronomyApp.Core.Enums;
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
  private readonly DeviceRevocationAnnouncer _revocationAnnouncer;
  private readonly EnrolmentInvitationService _service;
  private readonly DeviceTokenSplitter _tokenSplitter;

  public EnrolmentRedemptionHandler(EnrolmentInvitationService service,
                                    HubNotificationDispatcher dispatcher,
                                    OutstandingInvitationCache invitationCache,
                                    DeviceRevocationAnnouncer revocationAnnouncer,
                                    ResultEnvelope resultEnvelope,
                                    DeviceTokenSplitter tokenSplitter,
                                    ILogger<EnrolmentRedemptionHandler> log)
  {
    _service = service;
    _dispatcher = dispatcher;
    _invitationCache = invitationCache;
    _revocationAnnouncer = revocationAnnouncer;
    _resultEnvelope = resultEnvelope;
    _tokenSplitter = tokenSplitter;
    _log = log;
  }

  public async Task<IResult> RedeemAsync(RedeemEnrolmentRequest request, HttpContext httpContext, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(httpContext);

    if (string.IsNullOrWhiteSpace(request.Code))
    {
      _log.LogWarning("A phone asked to be set up without sending a code at all.");

      return _resultEnvelope.Problem(StatusCodes.Status400BadRequest, "ValidationFailed", "enrolment.codeMissing");
    }

    var redemption = await _service.RedeemAsync(new(request.Code, request.Name?.Trim(), request.UserAgent ?? string.Empty, httpContext.Request.Headers[AcceptLanguageHeaderName].ToString()), cancellationToken);

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
    _log.LogWarning("Enrolment refused for invitation {InvitationId}, because {Reason}.", redemption.InvitationId, reason);

    return _resultEnvelope.Problem(statusCode, code, messageKey);
  }

  private async Task<IResult> CompletedAsync(EnrolmentRedemptionResult redemption, string? previousDeviceToken, CancellationToken cancellationToken)
  {
    _invitationCache.Forget();

    var device = redemption.Device!;
    var deviceKind = redemption.OwnerKind!.Value;
    Guid ownerId;
    string ownerName;

    if (deviceKind == DeviceOwnerKind.StaffMember)
    {
      ownerId = redemption.StaffMember!.Id;
      ownerName = redemption.StaffMember!.Name;
    }
    else
    {
      ownerId = redemption.Station!.Id;
      ownerName = redemption.Station!.Name;
    }

    _log.LogInformation("Enrolment invitation {InvitationId} was redeemed. Device {DeviceId} now belongs to the {DeviceKind} {OwnerId}.", redemption.InvitationId, device.Id, deviceKind, ownerId);

    await RetireHandedOverDeviceAsync(previousDeviceToken, device.Id, cancellationToken);

    await _dispatcher.PushEnrolmentCompletedAsync(new(deviceKind, ownerId, ownerName, device.Id), cancellationToken);

    return Results.Ok(new RedeemedEnrolmentView(device.Id, redemption.PlaintextToken!, deviceKind, BuildStaffMemberView(redemption), BuildStationSummaryView(redemption), device.Language));
  }

  private StaffMemberView? BuildStaffMemberView(EnrolmentRedemptionResult redemption)
  {
    if (redemption.StaffMember is null)
      return null;

    return new(redemption.StaffMember.Id, redemption.StaffMember.Name);
  }

  private StationSummaryView? BuildStationSummaryView(EnrolmentRedemptionResult redemption)
  {
    if (redemption.Station is null)
      return null;

    return new(redemption.Station.Id, redemption.Station.Name);
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

    await _revocationAnnouncer.AnnounceAsync(retiredDeviceId, cancellationToken);
  }
}
