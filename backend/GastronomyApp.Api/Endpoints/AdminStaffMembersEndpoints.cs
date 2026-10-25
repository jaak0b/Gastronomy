using GastronomyApp.Api.Answers;
using GastronomyApp.Contracts.Admin.Staff;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class AdminStaffMembersEndpoints
{
  public static IEndpointRouteBuilder MapAdminStaffMembersEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/api/admin/staff-members");

    group.MapGet(string.Empty, async Task<ApiAnswer<AdminStaffMemberListView>> (StaffMemberService service, CancellationToken cancellationToken) => await service.ListAsync(cancellationToken));

    group.MapPut("/{staffMemberId:guid}", async Task<ApiAnswer<StaffMemberView>> (Guid staffMemberId, RenameStaffMemberRequest request, StaffMemberService service, CancellationToken cancellationToken) => await service.RenameAsync(staffMemberId, request, cancellationToken));

    group.MapPost("/{staffMemberId:guid}/activate",
                  async Task<ApiAnswer<StaffMemberView>> (Guid staffMemberId, ActivationService service, CancellationToken cancellationToken) => await service.ActivateAsync<StaffMember, StaffMemberView>(staffMemberId, Refusal.StaffMember.StaffMemberNotFound, cancellationToken));

    group.MapPost("/{staffMemberId:guid}/deactivate", async Task<ApiAnswer<StaffMemberView>> (Guid staffMemberId, StaffMemberService service, CancellationToken cancellationToken) => await service.DeactivateAsync(staffMemberId, cancellationToken));

    return routes;
  }
}
