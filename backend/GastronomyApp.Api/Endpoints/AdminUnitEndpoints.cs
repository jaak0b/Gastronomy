using GastronomyApp.Api.Answers;
using GastronomyApp.Contracts.Admin.Units;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class AdminUnitEndpoints
{
  public static IEndpointRouteBuilder MapAdminUnitEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/api/admin/units");

    group.MapGet(string.Empty, async Task<ApiAnswer<AdminUnitListView>> (UnitService service, CancellationToken cancellationToken) => await service.ListAsync(cancellationToken));

    group.MapPost(string.Empty, async Task<CreatedAnswer<AdminUnitView>> (SaveUnitRequest request, UnitService service, CancellationToken cancellationToken) => await service.CreateAsync(request, cancellationToken));

    group.MapPut("/{unitId:guid}", async Task<ApiAnswer<SavedUnitView>> (Guid unitId, SaveUnitRequest request, UnitService service, CancellationToken cancellationToken) => await service.UpdateAsync(unitId, request, cancellationToken));

    group.MapPost("/{unitId:guid}/activate", async Task<ApiAnswer<SavedUnitView>> (Guid unitId, ActivationService service, CancellationToken cancellationToken) => await service.ActivateAsync<Unit, SavedUnitView>(unitId, Refusal.Units.UnitNotFound, cancellationToken));

    group.MapPost("/{unitId:guid}/deactivate", async Task<ApiAnswer<SavedUnitView>> (Guid unitId, UnitService service, CancellationToken cancellationToken) => await service.DeactivateAsync(unitId, cancellationToken));

    return routes;
  }
}
