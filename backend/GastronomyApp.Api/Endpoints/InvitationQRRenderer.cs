using System.Text;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Hosting;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Http;
using QRCoder;

namespace GastronomyApp.Api.Endpoints;

public sealed class InvitationQRRenderer
{
  private const string SvgMediaType = "image/svg+xml";
  private const int PixelsPerModule = 8;

  private readonly OutstandingInvitationCache _invitationCache;
  private readonly ResultEnvelope _resultEnvelope;
  private readonly EnrolmentInvitationService _service;

  public InvitationQRRenderer(EnrolmentInvitationService service, OutstandingInvitationCache invitationCache, ResultEnvelope resultEnvelope)
  {
    _service = service;
    _invitationCache = invitationCache;
    _resultEnvelope = resultEnvelope;
  }

  public async Task<IResult> RenderAsync(Guid invitationId, HttpContext httpContext, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(httpContext);

    Result<OpenEnrolmentInvitation, EnrolmentInvitationFailure> stillOpen = await _service.EnsureStillOpenAsync(invitationId, cancellationToken);

    if (!stillOpen.IsSuccess)
      return RefusalFor(stillOpen.Failure);

    var remembered = _invitationCache.Read();

    if (remembered is null || remembered.InvitationId != invitationId)
      return BuildQRUnavailableProblem();

    httpContext.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
    httpContext.Response.Headers.Pragma = "no-cache";

    return Results.Text(Render(remembered.QRUrl), SvgMediaType, Encoding.UTF8);
  }

  private IResult RefusalFor(EnrolmentInvitationFailure failure)
  {
    return failure.Reason switch
           {
             EnrolmentInvitationFailureReason.InvitationUnknown => BuildQRUnavailableProblem(),
             EnrolmentInvitationFailureReason.InvitationReplaced => _resultEnvelope.Problem(StatusCodes.Status410Gone, "EnrolmentCodeReplaced", "admin.enrol.qrReplaced"),
             EnrolmentInvitationFailureReason.InvitationAlreadyUsed => _resultEnvelope.Problem(StatusCodes.Status410Gone, "EnrolmentCodeAlreadyUsed", "admin.enrol.qrAlreadyUsed"),
             EnrolmentInvitationFailureReason.InvitationExpired => _resultEnvelope.Problem(StatusCodes.Status410Gone, "EnrolmentCodeExpired", "admin.enrol.expired"),
             _ => new UnreachableCase().Throw<IResult>(failure.Reason)
           };
  }

  private IResult BuildQRUnavailableProblem()
  {
    return _resultEnvelope.Problem(StatusCodes.Status404NotFound, "EnrolmentCodeUnknown", "admin.enrol.qrUnavailable");
  }

  private string Render(string payload)
  {
    using QRCodeGenerator generator = new();
    using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
    SvgQRCode svg = new(data);

    return svg.GetGraphic(PixelsPerModule);
  }
}
