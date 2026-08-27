using System.Text;
using GastronomyApp.Api.Hosting;
using GastronomyApp.Core.Entities;
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
        routes.MapGet("/api/admin/enrolment/invitations/current/qr.svg", async (
            HttpContext httpContext,
            InvitationQrRenderer renderer,
            CancellationToken cancellationToken) => await renderer.RenderCurrentAsync(httpContext, cancellationToken));

        return routes;
    }
}

public sealed class InvitationQrRenderer
{
    private const string SvgMediaType = "image/svg+xml";
    private const int PixelsPerModule = 8;

    private readonly GastronomyAppDbContext dbContext;
    private readonly OutstandingInvitationCache invitationCache;
    private readonly IClock clock;

    public InvitationQrRenderer(
        GastronomyAppDbContext dbContext,
        OutstandingInvitationCache invitationCache,
        IClock clock)
    {
        this.dbContext = dbContext;
        this.invitationCache = invitationCache;
        this.clock = clock;
    }

    public async Task<IResult> RenderCurrentAsync(HttpContext httpContext, CancellationToken cancellationToken)
    {
        OutstandingInvitation? remembered = invitationCache.Read();

        if (remembered is null)
        {
            return Results.NotFound();
        }

        EnrolmentInvitation? invitation = await dbContext.EnrolmentInvitations
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == remembered.InvitationId, cancellationToken);

        if (invitation is null || invitation.ConsumedAtUtc is not null || invitation.ExpiresAtUtc <= clock.UtcNow)
        {
            invitationCache.Forget();

            return Results.NotFound();
        }

        httpContext.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        httpContext.Response.Headers.Pragma = "no-cache";

        return Results.Text(Render(remembered.QrUrl), SvgMediaType, Encoding.UTF8);
    }

    private string Render(string payload)
    {
        using QRCodeGenerator generator = new();
        using QRCodeData data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        SvgQRCode svg = new(data);

        return svg.GetGraphic(PixelsPerModule);
    }
}
