using GastronomyApp.Api.Auth;
using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class StationEndpoints
{
  public static IEndpointRouteBuilder MapStationEndpoints(this IEndpointRouteBuilder routes)
  {
    var stations = routes.MapGroup("/api/stations")
                         .RequireAuthorization()
                         .RequireStaffDevice()
                         .RequireRateLimiting(new RateLimitPolicyNames().PerDevice);

    stations.MapGet(string.Empty,
                    async (StationQueryHandler handler,
                           CancellationToken cancellationToken) => await handler.ListStationsAsync(cancellationToken));

    var estimates = routes.MapGroup("/api/estimates")
                          .RequireAuthorization()
                          .RequireStaffDevice()
                          .RequireRateLimiting(new RateLimitPolicyNames().PerDevice);

    estimates.MapGet(string.Empty,
                     async (StationEstimateHandler handler,
                            CancellationToken cancellationToken) => await handler.ListAsync(cancellationToken));

    var stationTablet = routes.MapGroup("/api/station")
                              .RequireAuthorization()
                              .RequireStationDevice()
                              .RequireRateLimiting(new RateLimitPolicyNames().PerDevice);

    stationTablet.MapGet("/orders",
                         async (HttpContext httpContext,
                                CallerIdentity callerIdentity,
                                StationQueueHandler handler,
                                CancellationToken cancellationToken) =>
                         {
                           var caller = callerIdentity.ReadStationDevice(httpContext.User)!;
                           return await handler.ListQueueAsync(caller, cancellationToken);
                         });

    stationTablet.MapGet("/orders/fulfilled",
                         async (HttpContext httpContext,
                                CallerIdentity callerIdentity,
                                StationQueueHandler handler,
                                CancellationToken cancellationToken) =>
                         {
                           var caller = callerIdentity.ReadStationDevice(httpContext.User)!;
                           return await handler.ListFulfilledAsync(caller, cancellationToken);
                         });

    stationTablet.MapPost("/items/fulfill",
                          async (StationItemSelectionRequest request,
                                 HttpContext httpContext,
                                 CallerIdentity callerIdentity,
                                 StationQueueHandler handler,
                                 CancellationToken cancellationToken) =>
                          {
                            var caller = callerIdentity.ReadStationDevice(httpContext.User)!;
                            return await handler.FulfillAsync(request, caller, cancellationToken);
                          });

    stationTablet.MapPost("/items/unfulfill",
                          async (StationItemSelectionRequest request,
                                 HttpContext httpContext,
                                 CallerIdentity callerIdentity,
                                 StationQueueHandler handler,
                                 CancellationToken cancellationToken) =>
                          {
                            var caller = callerIdentity.ReadStationDevice(httpContext.User)!;
                            return await handler.UnfulfillAsync(request, caller, cancellationToken);
                          });

    stationTablet.MapPost("/orders/{stationOrderId:guid}/hide",
                          async (Guid stationOrderId,
                                 HttpContext httpContext,
                                 CallerIdentity callerIdentity,
                                 StationQueueHandler handler,
                                 CancellationToken cancellationToken) =>
                          {
                            var caller = callerIdentity.ReadStationDevice(httpContext.User)!;
                            return await handler.HideAsync(stationOrderId, caller, cancellationToken);
                          });

    return routes;
  }
}

public sealed class StationShellResponder
{
  private readonly IWebHostEnvironment _environment;

  public StationShellResponder(IWebHostEnvironment environment)
  {
    _environment = environment;
  }

  public IResult Respond(HttpContext httpContext)
  {
    ArgumentNullException.ThrowIfNull(httpContext);

    var shellPath = Path.Combine(_environment.WebRootPath ?? string.Empty, "index.html");

    if (!File.Exists(shellPath))
    {
      return Results.NotFound();
    }

    httpContext.Response.Headers.CacheControl = "no-cache";

    return Results.File(shellPath, "text/html");
  }
}

public sealed class ClientRouteFallbackResponder
{
  private const string ApiPrefix = "/api";
  private const string HubPrefix = "/hub";

  private readonly StationShellResponder _shellResponder;

  public ClientRouteFallbackResponder(StationShellResponder shellResponder)
  {
    _shellResponder = shellResponder;
  }

  public IResult Respond(HttpContext httpContext)
  {
    ArgumentNullException.ThrowIfNull(httpContext);

    if (httpContext.Request.Path.StartsWithSegments(ApiPrefix)
        || httpContext.Request.Path.StartsWithSegments(HubPrefix))
    {
      return Results.NotFound();
    }

    return _shellResponder.Respond(httpContext);
  }
}
