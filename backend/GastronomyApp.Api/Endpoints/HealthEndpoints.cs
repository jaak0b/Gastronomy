using GastronomyApp.Api.Contracts;
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
        routes.MapGet("/api/health", async (
            HealthReporter reporter,
            GastronomyAppDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            return Results.Ok(await reporter.ReportAsync(dbContext, cancellationToken));
        }).AllowAnonymous();

        return routes;
    }
}

public sealed class HealthReporter
{
    public async Task<HealthView> ReportAsync(GastronomyAppDbContext dbContext, CancellationToken cancellationToken)
    {
        List<Guid> activeStationIds = await dbContext.Stations
            .AsNoTracking()
            .Where(station => station.IsActive)
            .Select(station => station.Id)
            .ToListAsync(cancellationToken);

        int printersOnline = await dbContext.PrinterStatuses
            .AsNoTracking()
            .CountAsync(
                status => activeStationIds.Contains(status.StationId) && status.IsOnline,
                cancellationToken);

        return new HealthView("ok", printersOnline, activeStationIds.Count);
    }
}
