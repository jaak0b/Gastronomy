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
  public async Task<HealthView> ReportAsync(GastronomyAppDbContext dbContext, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(dbContext);

    List<Station> activeStations = await dbContext.Stations
                                                  .AsNoTracking()
                                                  .Where(station => station.IsActive)
                                                  .ToListAsync(cancellationToken);

    return new("ok",
               activeStations.Count,
               activeStations.Count(station => station.DeviceId is not null));
  }
}
