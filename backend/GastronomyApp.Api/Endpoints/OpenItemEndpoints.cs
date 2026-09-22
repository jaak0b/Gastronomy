using GastronomyApp.Api.Auth.Conventions;
using GastronomyApp.Api.Handlers;
using GastronomyApp.Api.Values;
using GastronomyApp.Contracts.OpenItems;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class OpenItemEndpoints
{
  public static IEndpointRouteBuilder MapOpenItemEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/api/open-items").RequireAuthorization().RequireStaffDevice().RequireRateLimiting(Names.RateLimitPolicies.PerDevice);

    group.MapGet(string.Empty, async (OpenItemQueryHandler handler, CancellationToken cancellationToken) => await handler.ListAsync(cancellationToken));

    group.MapGet("/table-names", async (OpenItemQueryHandler handler, CancellationToken cancellationToken) => await handler.ListTableNamesAsync(cancellationToken));

    group.MapGet("/table", async (string? tableName, OpenItemQueryHandler handler, CancellationToken cancellationToken) => await handler.ReadTableAsync(tableName, cancellationToken));

    group.MapPost("/settle", async (SettleItemsRequest request, StaffDeviceCaller caller, OrderItemSettlementHandler handler, CancellationToken cancellationToken) => await handler.SettleAsync(request, caller, cancellationToken));

    return routes;
  }
}
