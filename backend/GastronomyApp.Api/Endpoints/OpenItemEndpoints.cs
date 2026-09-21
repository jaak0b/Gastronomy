using GastronomyApp.Api.Auth;
using GastronomyApp.Api.Auth.Conventions;
using GastronomyApp.Api.Handlers;
using GastronomyApp.Contracts.OpenItems;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class OpenItemEndpoints
{
  public static IEndpointRouteBuilder MapOpenItemEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/api/open-items").RequireAuthorization().RequireStaffDevice().RequireRateLimiting(Names.RateLimitPolicies.PerDevice);

    group.MapGet(string.Empty, async (OpenItemQueryHandler handler, CancellationToken cancellationToken) => { return await handler.ListAsync(cancellationToken); });

    group.MapGet("/table-names", async (OpenItemQueryHandler handler, CancellationToken cancellationToken) => { return await handler.ListTableNamesAsync(cancellationToken); });

    group.MapGet("/table", async (string? tableName, OpenItemQueryHandler handler, CancellationToken cancellationToken) => { return await handler.ReadTableAsync(tableName, cancellationToken); });

    group.MapPost("/settle",
                  async (SettleItemsRequest request, HttpContext httpContext, CallerIdentity callerIdentity, OrderItemSettlementHandler handler, CancellationToken cancellationToken) =>
                  {
                    var caller = callerIdentity.ReadStaffDevice(httpContext.User)!;
                    return await handler.SettleAsync(request, caller, cancellationToken);
                  });

    return routes;
  }
}
