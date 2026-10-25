using GastronomyApp.Api.Answers;
using GastronomyApp.Contracts.Admin.Stations;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class AdminStationEndpoints
{
  public static IEndpointRouteBuilder MapAdminStationEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/api/admin/stations");

    group.MapGet(string.Empty, async Task<ApiAnswer<AdminStationListView>> (Guid? festivalId, StationService service, CancellationToken cancellationToken) => await service.ListAsync(festivalId, cancellationToken));

    group.MapPost(string.Empty, async Task<CreatedAnswer<AdminStationView>> (SaveStationRequest request, StationService service, CancellationToken cancellationToken) => await service.CreateAsync(request, cancellationToken));

    group.MapPut("/{stationId:guid}", async Task<ApiAnswer<SavedStationView>> (Guid stationId, SaveStationRequest request, StationService service, CancellationToken cancellationToken) => await service.UpdateAsync(stationId, request, cancellationToken));

    group.MapPost("/{stationId:guid}/deactivate", async Task<ApiAnswer<SavedStationView>> (Guid stationId, StationService service, CancellationToken cancellationToken) => await service.DeactivateAsync(stationId, cancellationToken));

    group.MapPost("/{stationId:guid}/activate", async Task<ApiAnswer<SavedStationView>> (Guid stationId, ActivationService service, CancellationToken cancellationToken) => await service.ActivateAsync<Station, SavedStationView>(stationId, Refusal.Station.StationNotFound, cancellationToken));

    return routes;
  }
}
