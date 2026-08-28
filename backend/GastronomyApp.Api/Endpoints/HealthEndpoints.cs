using GastronomyApp.Api.Contracts;
using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Endpoints;

public static class HealthEndpoints
{
  public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder routes)
  {
    routes.MapGet("/api/health",
                  async (HealthReporter reporter,
                         GastronomyAppDbContext dbContext,
                         CancellationToken cancellationToken) =>
                  {
                    return Results.Ok(await reporter.ReportAsync(dbContext, cancellationToken));
                  })
          .AllowAnonymous();

    return routes;
  }
}

public sealed class HealthReporter
{
  private readonly StationPrinterStatusLookup statusLookup;

  public HealthReporter(StationPrinterStatusLookup statusLookup)
  {
    this.statusLookup = statusLookup;
  }

  public async Task<HealthView> ReportAsync(GastronomyAppDbContext dbContext, CancellationToken cancellationToken)
  {
    List<Guid> activeStationIds = await dbContext.Stations
                                                 .AsNoTracking()
                                                 .Where(station => station.IsActive)
                                                 .Select(station => station.Id)
                                                 .ToListAsync(cancellationToken);

    Dictionary<Guid, PrinterStatus> statuses =
      await statusLookup.ByStationAsync(dbContext, activeStationIds, cancellationToken);
    var printersOnline = statuses.Values
                                 .Where(status => status.IsOnline)
                                 .Select(status => status.PrinterId)
                                 .Distinct()
                                 .Count();

    return new("ok", printersOnline, activeStationIds.Count);
  }
}
