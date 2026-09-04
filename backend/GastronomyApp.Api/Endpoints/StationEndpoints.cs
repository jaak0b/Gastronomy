using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;
using GastronomyApp.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Endpoints;

public static class StationEndpoints
{
  public static IEndpointRouteBuilder MapStationEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/api/stations").RequireAuthorization();

    group.MapGet(string.Empty,
                 async (StationQueryHandler handler,
                        CancellationToken cancellationToken) =>
                 {
                   return await handler.ListStationsAsync(cancellationToken);
                 });

    group.MapGet("/{stationId:guid}/station-orders",
                 async (Guid stationId,
                        StationQueryHandler handler,
                        CancellationToken cancellationToken) =>
                 {
                   return await handler.ListStationOrdersAsync(stationId, cancellationToken);
                 });

    group.MapGet("/{stationId:guid}/status",
                 async (Guid stationId,
                        StationQueryHandler handler,
                        CancellationToken cancellationToken) =>
                 {
                   return await handler.StatusAsync(stationId, cancellationToken);
                 });

    group.MapPost("/{stationId:guid}/station-orders/{stationOrderId:guid}/hand-on-paper",
                  async (Guid stationOrderId,
                         StationHandOnPaperHandler handler,
                         CancellationToken cancellationToken) =>
                  {
                    return await handler.HandOnPaperAsync(stationOrderId, cancellationToken);
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
    if (httpContext.Request.Path.StartsWithSegments(ApiPrefix)
        || httpContext.Request.Path.StartsWithSegments(HubPrefix))
    {
      return Results.NotFound();
    }

    return _shellResponder.Respond();
  }
}

public sealed record StationPrintabilityRow(Guid StationId, StationPrintability Printability);

public sealed class StationPrintabilityReader
{
  private readonly StationPrinterStatusLookup _statusLookup;

  public StationPrintabilityReader(StationPrinterStatusLookup statusLookup)
  {
    _statusLookup = statusLookup;
  }

  public async Task<IReadOnlyDictionary<Guid, StationPrintability>> ReadAsync(GastronomyAppDbContext dbContext,
                                                                              CancellationToken cancellationToken)
  {
    List<Station> stations = await dbContext.Stations
                                            .AsNoTracking()
                                            .ToListAsync(cancellationToken);
    Dictionary<Guid, PrinterStatus> statuses = await _statusLookup.ByStationAsync(dbContext,
                                                                                 [.. stations.Select(station => station.Id)],
                                                                                 cancellationToken);

    Dictionary<Guid, StationPrintability> printability = [];

    foreach (var station in stations)
    {
      statuses.TryGetValue(station.Id, out var status);

      printability[station.Id] = new()
                                 {
                                   IsFaulty = status?.IsFaulty ?? true,
                                   IsOnline = status?.IsOnline ?? false,
                                   IsPaperEnd = status?.IsPaperEnd ?? false,
                                   IsCoverOpen = status?.IsCoverOpen ?? false,
                                   IsInErrorState = status?.IsInErrorState ?? false,
                                   IsEnabled = station.PrinterId is not null
                                 };
    }

    return printability;
  }

  public bool CanPrintRightNow(StationPrintability printability)
  {
    return !printability.IsFaulty
           && printability.IsOnline
           && !printability.IsPaperEnd
           && !printability.IsCoverOpen
           && !printability.IsInErrorState
           && printability.IsEnabled;
  }
}
