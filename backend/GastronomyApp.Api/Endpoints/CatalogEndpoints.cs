using GastronomyApp.Api.Auth.Conventions;
using GastronomyApp.Api.Handlers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class CatalogEndpoints
{
  public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder routes)
  {
    routes.MapGet("/api/catalog", async (CatalogHandler handler, CancellationToken cancellationToken) => await handler.ReadAsync(cancellationToken)).RequireAuthorization().RequireStaffDevice().RequireRateLimiting(Names.RateLimitPolicies.PerDevice);

    return routes;
  }
}
