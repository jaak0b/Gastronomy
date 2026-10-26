using GastronomyApp.Api.Answers;
using GastronomyApp.Contracts.Enrolment;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class AdminEnrolmentEndpoints
{
  public static IEndpointRouteBuilder MapAdminEnrolmentEndpoints(this IEndpointRouteBuilder routes)
  {
    routes.MapPost("/api/admin/enrolment/invitations", async Task<CreatedAnswer<InvitationView>> (CreateInvitationRequest request, EnrolmentService service, CancellationToken cancellationToken) => await service.CreateInvitationAsync(request.StaffMemberId, request.StationId, cancellationToken));

    return routes;
  }
}
