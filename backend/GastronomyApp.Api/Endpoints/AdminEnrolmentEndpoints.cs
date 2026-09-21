using GastronomyApp.Api.Handlers;
using GastronomyApp.Contracts.Enrolment;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class AdminEnrolmentEndpoints
{
  public static IEndpointRouteBuilder MapAdminEnrolmentEndpoints(this IEndpointRouteBuilder routes)
  {
    routes.MapPost("/api/admin/enrolment/invitations", async (CreateInvitationRequest request, AdminEnrolmentHandler handler, CancellationToken cancellationToken) => await handler.CreateInvitationAsync(request, cancellationToken)).Produces<InvitationView>(StatusCodes.Status201Created);

    return routes;
  }
}
