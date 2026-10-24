using GastronomyApp.Api.Auth.Conventions;
using GastronomyApp.Api.Handlers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class TablePlanEndpoints
{
  public static IEndpointRouteBuilder MapTablePlanEndpoints(this IEndpointRouteBuilder routes)
  {
    routes.MapGet("/api/table-plan", async (TablePlanHandler handler, CancellationToken cancellationToken) => await handler.ReadAsync(cancellationToken)).RequireAuthorization().RequireStaffDevice().RequireRateLimiting(Names.RateLimitPolicies.PerDevice);

    return routes;
  }
}
