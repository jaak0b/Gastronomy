using GastronomyApp.Api.Auth.Filters;
using GastronomyApp.Api.Handlers;
using GastronomyApp.Api.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class CatalogEndpoints
{
  public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder routes)
  {
    routes.MapGet("/api/catalog", async (CatalogHandler handler, CancellationToken cancellationToken) => await handler.ReadAsync(cancellationToken)).RequireAuthorization().RequireStaffDevice().RequireRateLimiting(new RateLimitPolicyNames().PerDevice);

    return routes;
  }
}
