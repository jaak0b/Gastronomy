using ErrorOr;
using GastronomyApp.Api.Answers;
using GastronomyApp.Api.Hosting;
using GastronomyApp.Contracts.Enrolment;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using MapsterMapper;

namespace GastronomyApp.Api.Handlers;

public sealed class AdminEnrolmentHandler
{
  private readonly IOutstandingInvitationCache _invitationCache;
  private readonly IMapper _mapper;
  private readonly EnrolmentService _service;
  private readonly EnrolmentUrlBuilder _urlBuilder;

  public AdminEnrolmentHandler(EnrolmentService service, EnrolmentUrlBuilder urlBuilder, IOutstandingInvitationCache invitationCache, IMapper mapper)
  {
    _service = service;
    _urlBuilder = urlBuilder;
    _invitationCache = invitationCache;
    _mapper = mapper;
  }

  public async Task<CreatedAnswer<InvitationView>> CreateInvitationAsync(CreateInvitationRequest request, CancellationToken cancellationToken)
  {
    return await _service.CreateAsync(request.StaffMemberId, request.StationId, cancellationToken).Then(RememberTheCodeAndDescribeTheInvitation);
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
