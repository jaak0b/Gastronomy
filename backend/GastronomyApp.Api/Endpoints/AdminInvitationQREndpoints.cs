using GastronomyApp.Api.Handlers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class AdminInvitationQREndpoints
{
  public static IEndpointRouteBuilder MapAdminInvitationQREndpoints(this IEndpointRouteBuilder routes)
  {
    routes.MapGet("/api/admin/enrolment/invitations/{invitationId:guid}/qr.svg", async (Guid invitationId, HttpContext httpContext, InvitationQRHandler renderer, CancellationToken cancellationToken) => await renderer.RenderAsync(invitationId, httpContext, cancellationToken));

    return routes;
  }
}
