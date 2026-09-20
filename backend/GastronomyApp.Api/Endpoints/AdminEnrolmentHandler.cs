using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Hosting;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Endpoints;

public sealed class AdminEnrolmentHandler
{
  private readonly OutstandingInvitationCache _invitationCache;
  private readonly ILogger<AdminEnrolmentHandler> _log;
  private readonly ResultEnvelope _resultEnvelope;
  private readonly DeviceRevocationAnnouncer _revocationAnnouncer;
  private readonly EnrolmentInvitationService _service;
  private readonly EnrolmentUrlBuilder _urlBuilder;

  public AdminEnrolmentHandler(EnrolmentInvitationService service, EnrolmentUrlBuilder urlBuilder, OutstandingInvitationCache invitationCache, DeviceRevocationAnnouncer revocationAnnouncer, ResultEnvelope resultEnvelope, ILogger<AdminEnrolmentHandler> log)
  {
    _service = service;
    _urlBuilder = urlBuilder;
    _invitationCache = invitationCache;
    _revocationAnnouncer = revocationAnnouncer;
    _resultEnvelope = resultEnvelope;
    _log = log;
  }

  public async Task<IResult> CreateInvitationAsync(CreateInvitationRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    Result<IssuedEnrolmentInvitation, EnrolmentInvitationFailure> issued = await _service.CreateAsync(request.StaffMemberId, request.StationId, cancellationToken);

    if (!issued.IsSuccess)
      return RefusalFor(issued.Failure);

    var invitation = issued.Value;
    var qrUrl = _urlBuilder.BuildEnrolmentUrl(invitation.QRCodeValue);
    _invitationCache.Remember(new(invitation.InvitationId, invitation.QRCodeValue, qrUrl, invitation.ExpiresAtUtc));

    _log.LogInformation("Enrolment invitation {InvitationId} was created for the {OwnerKind} {OwnerId} " + "at {Origin}, and is valid until {ExpiresAtUtc}. A missing owner means a waiter " + "who types their name when they scan it.",
                        invitation.InvitationId,
                        invitation.Owner?.Kind,
                        invitation.Owner?.Id,
                        _urlBuilder.Origin(),
                        invitation.ExpiresAtUtc);

    await _revocationAnnouncer.AnnounceAsync(invitation.RevokedDeviceId, cancellationToken);

    return Results.Json(BuildInvitationView(invitation, qrUrl), statusCode: StatusCodes.Status201Created);
  }

  private InvitationView BuildInvitationView(IssuedEnrolmentInvitation invitation, string qrUrl)
  {
    return new(invitation.InvitationId, qrUrl, invitation.ExpiresAtUtc, invitation.Owner?.Kind, BuildStaffMemberView(invitation), BuildStationSummaryView(invitation), _urlBuilder.ReachableAddresses());
  }

  private StaffMemberView? BuildStaffMemberView(IssuedEnrolmentInvitation invitation)
  {
    if (invitation.Owner?.Kind != DeviceOwnerKind.StaffMember)
      return null;

    return new(invitation.Owner.Id, invitation.OwnerName!);
  }

  private StationSummaryView? BuildStationSummaryView(IssuedEnrolmentInvitation invitation)
  {
    if (invitation.Owner?.Kind != DeviceOwnerKind.Station)
      return null;

    return new(invitation.Owner.Id, invitation.OwnerName!);
  }

  private IResult RefusalFor(EnrolmentInvitationFailure failure)
  {
    return failure.Reason switch
           {
             EnrolmentInvitationFailureReason.AtMostOneOwner => _resultEnvelope.Problem(StatusCodes.Status400BadRequest, "ValidationFailed", "enrolment.atMostOneOwner"),
             EnrolmentInvitationFailureReason.OwnerNotFound => Results.NotFound(),
             _ => new UnreachableCase().Throw<IResult>(failure.Reason)
           };
  }
}
