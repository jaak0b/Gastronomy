using ErrorOr;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Hosting;
using GastronomyApp.Contracts.Admin.Staff;
using GastronomyApp.Contracts.Enrolment;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Contracts.Stations;
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

    return await _service.CreateAsync(request.StaffMemberId, request.StationId, cancellationToken)
                         .Then(issuedInvitation => new IssuedEnrolmentCode(issuedInvitation, _urlBuilder.BuildEnrolmentUrl(issuedInvitation.QRCodeValue)))
                         .ThenDo(issuedCode =>
                                 {
                                   _invitationCache.Remember(new(issuedCode.Invitation.Invitation.Id, issuedCode.Invitation.QRCodeValue, issuedCode.QRUrl, issuedCode.Invitation.Invitation.ExpiresAtUtc));

                                   _log.LogInformation("Enrolment invitation {InvitationId} was created for the {OwnerKind} {OwnerId} at {Origin}, and is valid until {ExpiresAtUtc}. A missing owner means a waiter who types their name when they scan it.",
                                                       issuedCode.Invitation.Invitation.Id,
                                                       issuedCode.Invitation.Owner?.Kind,
                                                       issuedCode.Invitation.Owner?.Id,
                                                       _urlBuilder.Origin(),
                                                       issuedCode.Invitation.Invitation.ExpiresAtUtc);
                                 })
                         .Match(issuedCode => Results.Json(BuildInvitationView(issuedCode.Invitation, issuedCode.QRUrl), statusCode: StatusCodes.Status201Created), _resultEnvelope.Refuse);
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

  private sealed record IssuedEnrolmentCode(IssuedEnrolmentInvitation Invitation, string QRUrl);
}
