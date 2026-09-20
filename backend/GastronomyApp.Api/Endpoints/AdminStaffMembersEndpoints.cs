using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.Handlers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class AdminStaffMembersEndpoints
{
  public static IEndpointRouteBuilder MapAdminStaffMembersEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/api/admin/staff-members");

    group.MapGet(string.Empty, async (AdminStaffMembersHandler handler, CancellationToken cancellationToken) => await handler.ListAsync(cancellationToken));

    group.MapPut("/{staffMemberId:guid}", async (Guid staffMemberId, RenameStaffMemberRequest request, AdminStaffMembersHandler handler, CancellationToken cancellationToken) => await handler.RenameAsync(staffMemberId, request, cancellationToken));

    group.MapPost("/{staffMemberId:guid}/activate", async (Guid staffMemberId, AdminStaffMembersHandler handler, CancellationToken cancellationToken) => await handler.ActivateAsync(staffMemberId, cancellationToken));

    group.MapPost("/{staffMemberId:guid}/deactivate", async (Guid staffMemberId, AdminStaffMembersHandler handler, CancellationToken cancellationToken) => await handler.DeactivateAsync(staffMemberId, cancellationToken));

    return routes;
  }
}
