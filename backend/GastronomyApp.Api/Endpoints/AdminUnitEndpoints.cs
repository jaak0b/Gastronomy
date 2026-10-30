using GastronomyApp.Api.Answers;
using GastronomyApp.Contracts.Admin.Units;
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

    group.MapDelete("/{unitId:guid}", async Task<NoContentAnswer> (Guid unitId, UnitService service, CancellationToken cancellationToken) => await service.DeleteAsync(unitId, cancellationToken));

    return routes;
  }
}
