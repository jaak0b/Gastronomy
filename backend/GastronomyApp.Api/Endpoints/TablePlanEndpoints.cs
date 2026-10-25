using GastronomyApp.Api.Auth.Conventions;
using GastronomyApp.Api.Answers;
using GastronomyApp.Contracts.TablePlan;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class TablePlanEndpoints
{
  public static IEndpointRouteBuilder MapTablePlanEndpoints(this IEndpointRouteBuilder routes)
  {
    routes.MapGet("/api/table-plan", async Task<ApiAnswer<TablePlanView>> (TablePlanService service, CancellationToken cancellationToken) => await service.ReadRunningFestivalAsync(cancellationToken)).RequireAuthorization().RequireStaffDevice().RequireRateLimiting(Names.RateLimitPolicies.PerDevice);

    return routes;
  }
}
