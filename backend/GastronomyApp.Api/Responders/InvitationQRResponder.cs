using System.Text;
using ErrorOr;
using GastronomyApp.Api.ErrorHandling;
using Microsoft.AspNetCore.Http;
using QRCoder;

namespace GastronomyApp.Api.Responders;

public sealed class InvitationQRResponder
{
  private const string SvgMediaType = "image/svg+xml";
  private const int PixelsPerModule = 8;

  private readonly ResultEnvelope _resultEnvelope;

  public InvitationQRResponder(ResultEnvelope resultEnvelope)
  {
    _resultEnvelope = resultEnvelope;
  }

  public IResult Respond(ErrorOr<string> qrUrl, HttpContext httpContext)
  {
    ArgumentNullException.ThrowIfNull(httpContext);

    return qrUrl.Match(url => RenderTheCodePicture(url, httpContext), _resultEnvelope.Refuse);
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
