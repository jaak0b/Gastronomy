using GastronomyApp.Api.Handlers;
using GastronomyApp.Contracts.Admin.Staff;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class AdminStaffMembersEndpoints
{
  public static IEndpointRouteBuilder MapAdminStaffMembersEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/api/admin/staff-members");

    group.MapGet(string.Empty, async (AdminStaffMembersHandler handler, CancellationToken cancellationToken) => await handler.ListAsync(cancellationToken)).Produces<AdminStaffMemberListView>();

    group.MapPut("/{staffMemberId:guid}", async (Guid staffMemberId, RenameStaffMemberRequest request, AdminStaffMembersHandler handler, CancellationToken cancellationToken) => await handler.RenameAsync(staffMemberId, request, cancellationToken)).Produces<StaffMemberView>();

    group.MapPost("/{staffMemberId:guid}/activate", async (Guid staffMemberId, AdminStaffMembersHandler handler, CancellationToken cancellationToken) => await handler.ActivateAsync(staffMemberId, cancellationToken)).Produces<StaffMemberView>();

    group.MapPost("/{staffMemberId:guid}/deactivate", async (Guid staffMemberId, AdminStaffMembersHandler handler, CancellationToken cancellationToken) => await handler.DeactivateAsync(staffMemberId, cancellationToken)).Produces<StaffMemberView>();

    return routes;
  }
}
