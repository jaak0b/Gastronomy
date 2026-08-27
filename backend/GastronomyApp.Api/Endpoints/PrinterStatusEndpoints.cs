using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.RateLimiting;
using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Endpoints;

public static class PrinterStatusEndpoints
{
    public static IEndpointRouteBuilder MapPrinterStatusEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/printers/status", async (
            PrinterStatusReader reader,
            GastronomyAppDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            return Results.Ok(await reader.ReadAsync(dbContext, cancellationToken));
        }).RequireAuthorization().RequireRateLimiting(new RateLimitPolicyNames().PerDevice);

        return routes;
    }
}

public sealed class PrinterStatusReader
{
    public async Task<PrinterStatusListView> ReadAsync(
        GastronomyAppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        List<Station> stations = await dbContext.Stations
            .AsNoTracking()
            .Where(station => station.IsActive)
            .OrderBy(station => station.SortOrder)
            .ToListAsync(cancellationToken);

        HashSet<Guid> stationIds = [.. stations.Select(station => station.Id)];

        Dictionary<Guid, PrinterStatus> statuses = await dbContext.PrinterStatuses
            .AsNoTracking()
            .Where(status => stationIds.Contains(status.StationId))
            .ToDictionaryAsync(status => status.StationId, cancellationToken);

        List<PrinterStatusView> views = [];

        foreach (Station station in stations)
        {
            statuses.TryGetValue(station.Id, out PrinterStatus? status);

            views.Add(new PrinterStatusView(
                station.Id,
                station.Name,
                status?.IsOnline ?? false,
                status?.IsPaperEnd ?? false,
                status?.IsPaperNearEnd ?? false,
                status?.IsCoverOpen ?? false,
                status?.IsFaulty ?? false,
                status?.LastChangedAtUtc ?? default));
        }

        return new PrinterStatusListView(views);
    }
}
