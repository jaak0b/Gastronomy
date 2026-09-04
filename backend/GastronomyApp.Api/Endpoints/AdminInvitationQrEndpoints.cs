using System.Text;
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
    routes.MapGet("/api/admin/enrolment/invitations/current/qr.svg",
                  async (HttpContext httpContext,
                         InvitationQrRenderer renderer,
                         CancellationToken cancellationToken) => await renderer.RenderCurrentAsync(httpContext, cancellationToken));

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

  public InvitationQrRenderer(GastronomyAppDbContext dbContext,
                              OutstandingInvitationCache invitationCache,
                              IClock clock)
  {
    _dbContext = dbContext;
    _invitationCache = invitationCache;
    _clock = clock;
  }

  public async Task<IResult> RenderCurrentAsync(HttpContext httpContext, CancellationToken cancellationToken)
  {
    var remembered = _invitationCache.Read();

    if (remembered is null)
    {
      return Results.NotFound();
    }

    var invitation = await _dbContext.EnrolmentInvitations
                                    .AsNoTracking()
                                    .FirstOrDefaultAsync(candidate => candidate.Id == remembered.InvitationId, cancellationToken);

    if (invitation is null || invitation.ConsumedAtUtc is not null || invitation.ExpiresAtUtc <= _clock.UtcNow)
    {
      _invitationCache.Forget();

      return Results.NotFound();
    }

    httpContext.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
    httpContext.Response.Headers.Pragma = "no-cache";

    return Results.Text(Render(remembered.QrUrl), SvgMediaType, Encoding.UTF8);
  }

  private string Render(string payload)
  {
    using QRCodeGenerator generator = new();
    using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
    SvgQRCode svg = new(data);

    return svg.GetGraphic(PixelsPerModule);
  }
}
