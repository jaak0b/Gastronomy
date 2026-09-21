using ErrorOr;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Hosting;
using GastronomyApp.Contracts.Enrolment;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using MapsterMapper;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Handlers;

public sealed class AdminEnrolmentHandler
{
  private readonly IOutstandingInvitationCache _invitationCache;
  private readonly IMapper _mapper;
  private readonly ResultEnvelope _resultEnvelope;
  private readonly EnrolmentInvitationService _service;
  private readonly EnrolmentUrlBuilder _urlBuilder;

  public AdminEnrolmentHandler(EnrolmentInvitationService service, EnrolmentUrlBuilder urlBuilder, IOutstandingInvitationCache invitationCache, ResultEnvelope resultEnvelope, IMapper mapper)
  {
    _service = service;
    _urlBuilder = urlBuilder;
    _invitationCache = invitationCache;
    _resultEnvelope = resultEnvelope;
    _mapper = mapper;
  }

  public async Task<IResult> CreateInvitationAsync(CreateInvitationRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    return await _service.CreateAsync(request.StaffMemberId, request.StationId, cancellationToken).Then(issuedInvitation => RememberTheCodeAndDescribeTheInvitation(issuedInvitation)).Match(view => Results.Json(view, statusCode: StatusCodes.Status201Created), _resultEnvelope.Refuse);
  }

  private InvitationView RememberTheCodeAndDescribeTheInvitation(IssuedEnrolmentInvitation issuedInvitation)
  {
    var qrUrl = _urlBuilder.BuildEnrolmentUrl(issuedInvitation.QRCodeValue);

    _invitationCache.Remember(new(issuedInvitation.Invitation.Id, issuedInvitation.QRCodeValue, qrUrl, issuedInvitation.Invitation.ExpiresAtUtc));

    return _mapper.Map<InvitationView>(issuedInvitation) with
           {
             QRUrl = qrUrl,
             AvailableAddresses = _urlBuilder.ReachableAddresses()
           };
  }
}
