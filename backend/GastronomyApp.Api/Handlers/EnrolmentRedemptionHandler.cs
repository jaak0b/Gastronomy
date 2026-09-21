using ErrorOr;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Contracts.Enrolment;
using GastronomyApp.Core.Services;
using MapsterMapper;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Handlers;

public sealed class EnrolmentRedemptionHandler
{
  private const string AcceptLanguageHeaderName = "Accept-Language";

  private readonly IMapper _mapper;
  private readonly ResultEnvelope _resultEnvelope;
  private readonly EnrolmentInvitationService _service;

  public EnrolmentRedemptionHandler(EnrolmentInvitationService service, ResultEnvelope resultEnvelope, IMapper mapper)
  {
    _service = service;
    _resultEnvelope = resultEnvelope;
    _mapper = mapper;
  }

  public async Task<IResult> RedeemAsync(RedeemEnrolmentRequest request, HttpContext httpContext, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(httpContext);

    return await _service.RedeemAsync(request.Code!, request.Name?.Trim(), request.UserAgent ?? string.Empty, httpContext.Request.Headers[AcceptLanguageHeaderName].ToString(), request.PreviousDeviceToken, cancellationToken)
                         .Match(redemption => Results.Ok(_mapper.Map<RedeemedEnrolmentView>(redemption)), _resultEnvelope.Refuse);
  }
}
