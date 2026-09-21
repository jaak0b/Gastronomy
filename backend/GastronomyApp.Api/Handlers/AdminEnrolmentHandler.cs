using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Hosting;
using GastronomyApp.Contracts;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Handlers;

public sealed class AdminEnrolmentHandler
{
  private readonly OutstandingInvitationCache _invitationCache;
  private readonly ILogger<AdminEnrolmentHandler> _log;
  private readonly ResultEnvelope _resultEnvelope;
  private readonly EnrolmentInvitationService _service;
  private readonly EnrolmentUrlBuilder _urlBuilder;

  public AdminEnrolmentHandler(EnrolmentInvitationService service, EnrolmentUrlBuilder urlBuilder, OutstandingInvitationCache invitationCache, ResultEnvelope resultEnvelope, ILogger<AdminEnrolmentHandler> log)
  {
    _service = service;
    _urlBuilder = urlBuilder;
    _invitationCache = invitationCache;
    _resultEnvelope = resultEnvelope;
    _log = log;
  }

  public async Task<IResult> CreateInvitationAsync(CreateInvitationRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    Result<IssuedEnrolmentInvitation, Failure<EnrolmentInvitationFailureReason>> issued = await _service.CreateAsync(request.StaffMemberId, request.StationId, cancellationToken);

    if (!issued.IsSuccess)
      return RefusalFor(issued.Failure);

    var issuedInvitation = issued.Value;
    var invitation = issuedInvitation.Invitation;
    var qrUrl = _urlBuilder.BuildEnrolmentUrl(issuedInvitation.QRCodeValue);
    _invitationCache.Remember(new(invitation.Id, issuedInvitation.QRCodeValue, qrUrl, invitation.ExpiresAtUtc));

    _log.LogInformation("Enrolment invitation {InvitationId} was created for the {OwnerKind} {OwnerId} at {Origin}, and is valid until {ExpiresAtUtc}. A missing owner means a waiter who types their name when they scan it.",
                        invitation.Id,
                        issuedInvitation.Owner?.Kind,
                        issuedInvitation.Owner?.Id,
                        _urlBuilder.Origin(),
                        invitation.ExpiresAtUtc);

    return Results.Json(BuildInvitationView(issuedInvitation, qrUrl), statusCode: StatusCodes.Status201Created);
  }

  private InvitationView BuildInvitationView(IssuedEnrolmentInvitation issuedInvitation, string qrUrl)
  {
    return new(issuedInvitation.Invitation.Id, qrUrl, issuedInvitation.Invitation.ExpiresAtUtc, issuedInvitation.Owner?.Kind, BuildStaffMemberView(issuedInvitation), BuildStationSummaryView(issuedInvitation), _urlBuilder.ReachableAddresses());
  }

  private StaffMemberView? BuildStaffMemberView(IssuedEnrolmentInvitation issuedInvitation)
  {
    if (issuedInvitation.Owner?.Kind != DeviceOwnerKind.StaffMember)
      return null;

    return new(issuedInvitation.Owner.Id, issuedInvitation.Owner.Name);
  }

  private StationSummaryView? BuildStationSummaryView(IssuedEnrolmentInvitation issuedInvitation)
  {
    if (issuedInvitation.Owner?.Kind != DeviceOwnerKind.Station)
      return null;

    return new(issuedInvitation.Owner.Id, issuedInvitation.Owner.Name);
  }

  private IResult RefusalFor(Failure<EnrolmentInvitationFailureReason> failure)
  {
    return failure.Reason switch
           {
             EnrolmentInvitationFailureReason.AtMostOneOwner => _resultEnvelope.Problem(StatusCodes.Status400BadRequest, "ValidationFailed", "enrolment.atMostOneOwner"),
             EnrolmentInvitationFailureReason.OwnerNotFound => Results.NotFound(),
             _ => new UnreachableCase().Throw<IResult>(failure.Reason)
           };
  }
}
