using GastronomyApp.Api.Answers;
using GastronomyApp.Api.Auth.Conventions;
using GastronomyApp.Api.Values;
using GastronomyApp.Contracts.OpenItems;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class OpenItemEndpoints
{
  public static IEndpointRouteBuilder MapOpenItemEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/api/open-items").RequireAuthorization().RequireStaffDevice().RequireRateLimiting(Names.RateLimitPolicies.PerDevice);

    group.MapPost("/tables/{tableId:guid}/search",
                  async Task<ApiAnswer<TableOrderReportView>> (Guid tableId, TableOrderSearchRequest search, OpenItemsService service, CancellationToken cancellationToken) =>
                    await service.SearchTableAsync(tableId, search.OrderStatus, search.PaymentStatus, search.IncludeCancelled, cancellationToken));

    group.MapPost("/settle",
                  async Task<ApiAnswer<SettlementView>> (SettleItemsRequest request, StaffDeviceCaller caller, OpenItemsService service, CancellationToken cancellationToken) => await service.SettleAsync(request.Lines ?? [], request.PaymentMethod!.Value, caller.StaffMemberId, cancellationToken));

    group.MapPost("/cancel", async Task<ApiAnswer<CancellationView>> (CancelItemsRequest request, StaffDeviceCaller caller, OpenItemsService service, CancellationToken cancellationToken) => await service.CancelAsync(request.OrderItemIds ?? [], caller.StaffMemberId, cancellationToken));

    return routes;
  }
}
