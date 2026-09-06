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

    stationTablet.MapPost("/items/status",
                          async (StationItemStatusRequest request,
                                 HttpContext httpContext,
                                 CallerIdentity callerIdentity,
                                 StationQueueHandler handler,
                                 CancellationToken cancellationToken) =>
                          {
                            var caller = callerIdentity.ReadStationDevice(httpContext.User)!;
                            return await handler.AdvanceAsync(request, caller, cancellationToken);
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

  public IResult Respond()
  {
    var shellPath = Path.Combine(_environment.WebRootPath ?? string.Empty, "index.html");

    return File.Exists(shellPath)
             ? Results.File(shellPath, "text/html")
             : Results.NotFound();
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

    return _shellResponder.Respond();
  }
}
