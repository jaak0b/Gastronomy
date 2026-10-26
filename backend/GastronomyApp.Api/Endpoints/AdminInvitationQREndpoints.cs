using GastronomyApp.Api.Responders;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class AdminInvitationQREndpoints
{
  public static IEndpointRouteBuilder MapAdminInvitationQREndpoints(this IEndpointRouteBuilder routes)
  {
    routes.MapGet("/api/admin/enrolment/invitations/{invitationId:guid}/qr.svg",
                  async (Guid invitationId, HttpContext httpContext, EnrolmentService service, InvitationQRResponder responder, CancellationToken cancellationToken) => responder.Respond(await service.ReadOpenQRUrlAsync(invitationId, cancellationToken), httpContext))
          .Produces(StatusCodes.Status200OK, contentType: "image/svg+xml");

    return routes;
  }
}
