using System.Text;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Hosting;
using GastronomyApp.Core.Ports;
using GastronomyApp.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using QRCoder;

namespace GastronomyApp.Api.Endpoints;

public static class AdminInvitationQrEndpoints
{
  public static IEndpointRouteBuilder MapAdminInvitationQrEndpoints(this IEndpointRouteBuilder routes)
  {
    routes.MapGet("/api/admin/enrolment/invitations/{invitationId:guid}/qr.svg",
                  async (Guid invitationId,
                         HttpContext httpContext,
                         InvitationQrRenderer renderer,
                         CancellationToken cancellationToken) =>
                    await renderer.RenderAsync(invitationId, httpContext, cancellationToken));

    return routes;
  }
}

public sealed class InvitationQrRenderer
{
  private const string SvgMediaType = "image/svg+xml";
  private const int PixelsPerModule = 8;
  private readonly IClock _clock;

  private readonly GastronomyAppDbContext _dbContext;
  private readonly OutstandingInvitationCache _invitationCache;
  private readonly ResultEnvelope _resultEnvelope;

  public InvitationQrRenderer(GastronomyAppDbContext dbContext,
                              OutstandingInvitationCache invitationCache,
                              ResultEnvelope resultEnvelope,
                              IClock clock)
  {
    _dbContext = dbContext;
    _invitationCache = invitationCache;
    _resultEnvelope = resultEnvelope;
    _clock = clock;
  }

  public async Task<IResult> RenderAsync(Guid invitationId,
                                         HttpContext httpContext,
                                         CancellationToken cancellationToken)
  {
    var invitation = await _dbContext.EnrolmentInvitations
                                    .AsNoTracking()
                                    .FirstOrDefaultAsync(candidate => candidate.Id == invitationId, cancellationToken);

    if (invitation is null)
    {
      return NoLongerShowable();
    }

    if (invitation.ConsumedAtUtc is not null)
    {
      return invitation.ConsumedByDeviceId is null
               ? _resultEnvelope.Problem(StatusCodes.Status410Gone,
                                         "EnrolmentCodeReplaced",
                                         "admin.enrol.qrReplaced")
               : _resultEnvelope.Problem(StatusCodes.Status410Gone,
                                         "EnrolmentCodeAlreadyUsed",
                                         "admin.enrol.qrAlreadyUsed");
    }

    if (invitation.ExpiresAtUtc <= _clock.UtcNow)
    {
      return _resultEnvelope.Problem(StatusCodes.Status410Gone,
                                    "EnrolmentCodeExpired",
                                    "admin.enrol.expired");
    }

    var remembered = _invitationCache.Read();

    if (remembered is null || remembered.InvitationId != invitationId)
    {
      return NoLongerShowable();
    }

    httpContext.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
    httpContext.Response.Headers.Pragma = "no-cache";

    return Results.Text(Render(remembered.QrUrl), SvgMediaType, Encoding.UTF8);
  }

  private IResult NoLongerShowable()
  {
    return _resultEnvelope.Problem(StatusCodes.Status404NotFound,
                                  "EnrolmentCodeUnknown",
                                  "admin.enrol.qrUnavailable");
  }

  private string Render(string payload)
  {
    using QRCodeGenerator generator = new();
    using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
    SvgQRCode svg = new(data);

    return svg.GetGraphic(PixelsPerModule);
  }
}
