using System.Text;
using ErrorOr;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Hosting;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Http;
using QRCoder;

namespace GastronomyApp.Api.Handlers;

public sealed class InvitationQRHandler
{
  private const string SvgMediaType = "image/svg+xml";
  private const int PixelsPerModule = 8;

  private readonly OutstandingInvitationCache _invitationCache;
  private readonly ResultEnvelope _resultEnvelope;
  private readonly EnrolmentInvitationService _service;

  public InvitationQRHandler(EnrolmentInvitationService service, OutstandingInvitationCache invitationCache, ResultEnvelope resultEnvelope)
  {
    _service = service;
    _invitationCache = invitationCache;
    _resultEnvelope = resultEnvelope;
  }

  public async Task<IResult> RenderAsync(Guid invitationId, HttpContext httpContext, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(httpContext);

    return await _service.EnsureStillOpenAsync(invitationId, cancellationToken)
                         .Match(invitation => RenderTheRememberedCode(invitationId, httpContext), _resultEnvelope.Refuse);
  }

  private IResult RenderTheRememberedCode(Guid invitationId, HttpContext httpContext)
  {
    var remembered = _invitationCache.Read();

    if (remembered is null || remembered.InvitationId != invitationId)
      return BuildQRUnavailableProblem();

    httpContext.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
    httpContext.Response.Headers.Pragma = "no-cache";

    return Results.Text(Render(remembered.QRUrl), SvgMediaType, Encoding.UTF8);
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
