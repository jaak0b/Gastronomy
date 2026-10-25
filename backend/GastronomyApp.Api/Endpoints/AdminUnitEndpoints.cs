using GastronomyApp.Api.Handlers;
using GastronomyApp.Contracts.Admin.Units;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class AdminUnitEndpoints
{
  public static IEndpointRouteBuilder MapAdminUnitEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/api/admin/units");

    group.MapGet(string.Empty, async (AdminUnitHandler handler, CancellationToken cancellationToken) => await handler.ListAsync(cancellationToken));

    group.MapPost(string.Empty, async (SaveUnitRequest request, AdminUnitHandler handler, CancellationToken cancellationToken) => await handler.CreateAsync(request, cancellationToken));

    group.MapPut("/{unitId:guid}", async (Guid unitId, SaveUnitRequest request, AdminUnitHandler handler, CancellationToken cancellationToken) => await handler.UpdateAsync(unitId, request, cancellationToken));

    group.MapPost("/{unitId:guid}/activate", async (Guid unitId, AdminUnitHandler handler, CancellationToken cancellationToken) => await handler.ActivateAsync(unitId, cancellationToken));

    group.MapPost("/{unitId:guid}/deactivate", async (Guid unitId, AdminUnitHandler handler, CancellationToken cancellationToken) => await handler.DeactivateAsync(unitId, cancellationToken));

    return routes;
  }
}
