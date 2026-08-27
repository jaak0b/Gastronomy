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
        EventSession? session = await dbContext.EventSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.IsActive, cancellationToken);

        List<Guid> activeLocationIds = await dbContext.ProductionLocations
            .AsNoTracking()
            .Where(location => location.IsActive)
            .Select(location => location.Id)
            .ToListAsync(cancellationToken);

        int printersOnline = await dbContext.PrinterStatuses
            .AsNoTracking()
            .CountAsync(
                status => activeLocationIds.Contains(status.ProductionLocationId) && status.IsOnline,
                cancellationToken);

        return new HealthView("ok", session?.Name, printersOnline, activeLocationIds.Count);
    }
}
