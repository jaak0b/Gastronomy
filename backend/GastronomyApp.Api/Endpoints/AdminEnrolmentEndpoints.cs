using GastronomyApp.Api.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class AdminEnrolmentEndpoints
{
  public static IEndpointRouteBuilder MapAdminEnrolmentEndpoints(this IEndpointRouteBuilder routes)
  {
    routes.MapPost("/api/admin/enrolment/invitations",
                   async (CreateInvitationRequest request,
                          AdminEnrolmentHandler handler,
                          CancellationToken cancellationToken) => await handler.CreateInvitationAsync(request, cancellationToken));

    return routes;
  }
}
