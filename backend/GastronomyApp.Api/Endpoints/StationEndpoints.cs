using GastronomyApp.Api.Auth.Conventions;
using ErrorOr;
using GastronomyApp.Api.Answers;
using GastronomyApp.Api.Values;
using GastronomyApp.Contracts.Orders;
using GastronomyApp.Contracts.Stations;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class StationEndpoints
{
  public static IEndpointRouteBuilder MapStationEndpoints(this IEndpointRouteBuilder routes)
  {
    var estimates = routes.MapGroup("/api/estimates").RequireAuthorization().RequireStaffDevice().RequireRateLimiting(Names.RateLimitPolicies.PerDevice);

    estimates.MapGet(string.Empty, async Task<ApiAnswer<IReadOnlyList<ItemEstimateView>>> (EstimateService service, CancellationToken cancellationToken) => new((await service.ListAsync(cancellationToken)).ToErrorOr()));

    estimates.MapPost("/quote", async Task<ApiAnswer<EstimateQuoteView>> (EstimateQuoteRequest request, EstimateService service, CancellationToken cancellationToken) => await service.QuoteAsync(request, cancellationToken));

    var stationTablet = routes.MapGroup("/api/station").RequireAuthorization().RequireStationDevice().RequireRateLimiting(Names.RateLimitPolicies.PerDevice);

    stationTablet.MapGet("/orders", async Task<ApiAnswer<StationQueueView>> (StationDeviceCaller caller, StationQueueService service, CancellationToken cancellationToken) => await service.ReadQueueAsync(caller.StationId, cancellationToken));

    stationTablet.MapGet("/orders/fulfilled", async Task<ApiAnswer<StationFulfilledView>> (StationDeviceCaller caller, StationQueueService service, CancellationToken cancellationToken) => await service.ReadFulfilledAsync(caller.StationId, cancellationToken));

    stationTablet.MapPost("/items/fulfill",
                          async Task<ApiAnswer<StationQueueView>> (StationItemSelectionRequest request, StationDeviceCaller caller, StationQueueService service, CancellationToken cancellationToken) => await service.FulfillAsync(request.OrderItemIds ?? [], caller.StationId, cancellationToken));

    stationTablet.MapPost("/items/unfulfill",
                          async Task<ApiAnswer<StationQueueView>> (StationItemSelectionRequest request, StationDeviceCaller caller, StationQueueService service, CancellationToken cancellationToken) => await service.UnfulfillAsync(request.OrderItemIds ?? [], caller.StationId, cancellationToken));

    stationTablet.MapPost("/items/acknowledge-cancellation",
                          async Task<ApiAnswer<StationQueueView>> (StationItemSelectionRequest request, StationDeviceCaller caller, StationQueueService service, CancellationToken cancellationToken) =>
                            await service.AcknowledgeCancellationAsync(request.OrderItemIds ?? [], caller.StationId, cancellationToken));

    stationTablet.MapPost("/orders/{stationOrderId:guid}/hide",
                          async Task<ApiAnswer<StationQueueView>> (Guid stationOrderId, StationDeviceCaller caller, StationQueueService service, CancellationToken cancellationToken) => await service.HideFromAsItComesQueueAsync(stationOrderId, caller.StationId, cancellationToken));

    return routes;
  }
}
