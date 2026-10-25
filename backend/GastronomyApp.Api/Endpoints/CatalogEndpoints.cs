using GastronomyApp.Api.Auth.Conventions;
using GastronomyApp.Api.Answers;
using GastronomyApp.Contracts.Catalog;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class CatalogEndpoints
{
  public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder routes)
  {
    routes.MapGet("/api/catalog", async Task<ApiAnswer<CatalogView>> (CatalogService service, CancellationToken cancellationToken) => await service.ReadRunningFestivalAsync(cancellationToken)).RequireAuthorization().RequireStaffDevice().RequireRateLimiting(Names.RateLimitPolicies.PerDevice);

    return routes;
  }
}
