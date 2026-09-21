using GastronomyApp.Api.Handlers;
using GastronomyApp.Contracts.Admin.Stations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class AdminStationEndpoints
{
  public static IEndpointRouteBuilder MapAdminStationEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/api/admin/stations");

    group.MapGet(string.Empty, async (Guid? festivalId, AdminStationHandler handler, CancellationToken cancellationToken) => await handler.ListAsync(festivalId, cancellationToken));

    group.MapPost(string.Empty, async (SaveStationRequest request, AdminStationHandler handler, CancellationToken cancellationToken) => await handler.CreateAsync(request, cancellationToken));

    group.MapPut("/{stationId:guid}", async (Guid stationId, SaveStationRequest request, AdminStationHandler handler, CancellationToken cancellationToken) => await handler.UpdateAsync(stationId, request, cancellationToken));

    group.MapPost("/{stationId:guid}/deactivate", async (Guid stationId, AdminStationHandler handler, CancellationToken cancellationToken) => await handler.DeactivateAsync(stationId, cancellationToken));

    group.MapPost("/{stationId:guid}/activate", async (Guid stationId, AdminStationHandler handler, CancellationToken cancellationToken) => await handler.ActivateAsync(stationId, cancellationToken));

    return routes;
  }
}
