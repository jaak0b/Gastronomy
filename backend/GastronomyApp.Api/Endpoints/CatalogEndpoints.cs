using GastronomyApp.Api.Auth.Conventions;
using GastronomyApp.Api.Handlers;
using GastronomyApp.Contracts.Catalog;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class CatalogEndpoints
{
  public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder routes)
  {
    routes.MapGet("/api/catalog", async (CatalogHandler handler, CancellationToken cancellationToken) => await handler.ReadAsync(cancellationToken)).RequireAuthorization().RequireStaffDevice().RequireRateLimiting(Names.RateLimitPolicies.PerDevice).Produces<CatalogView>();

    return routes;
  }
}
