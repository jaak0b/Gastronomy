using System.Text;
using ErrorOr;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Http;
using QRCoder;

namespace GastronomyApp.Api.Handlers;

public sealed class InvitationQRHandler
{
  private const string SvgMediaType = "image/svg+xml";
  private const int PixelsPerModule = 8;

  private readonly ResultEnvelope _resultEnvelope;
  private readonly EnrolmentInvitationService _service;

  public InvitationQRHandler(EnrolmentInvitationService service, ResultEnvelope resultEnvelope)
  {
    _service = service;
    _resultEnvelope = resultEnvelope;
  }

  public async Task<IResult> RenderAsync(Guid invitationId, HttpContext httpContext, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(httpContext);

    return await _service.ReadOpenQRUrlAsync(invitationId, cancellationToken).Match(qrUrl => RenderTheCodePicture(qrUrl, httpContext), _resultEnvelope.Refuse);
  }

  private IResult RenderTheCodePicture(string qrUrl, HttpContext httpContext)
  {
    httpContext.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
    httpContext.Response.Headers.Pragma = "no-cache";

    using QRCodeGenerator generator = new();
    using var data = generator.CreateQrCode(qrUrl, QRCodeGenerator.ECCLevel.Q);
    SvgQRCode svg = new(data);

    return Results.Text(svg.GetGraphic(PixelsPerModule), SvgMediaType, Encoding.UTF8);
  }
}
