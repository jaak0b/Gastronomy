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
        RouteGroupBuilder group = routes.MapGroup("/api/stations").RequireAuthorization();

        group.MapGet(string.Empty, async (
            StationQueryHandler handler,
            CancellationToken cancellationToken) =>
        {
            return await handler.ListLocationsAsync(cancellationToken);
        });

        group.MapGet("/{locationId:guid}/tickets", async (
            Guid locationId,
            StationQueryHandler handler,
            CancellationToken cancellationToken) =>
        {
            return await handler.ListTicketsAsync(locationId, cancellationToken);
        });

        group.MapGet("/{locationId:guid}/status", async (
            Guid locationId,
            StationQueryHandler handler,
            CancellationToken cancellationToken) =>
        {
            return await handler.StatusAsync(locationId, cancellationToken);
        });

        group.MapPost("/{locationId:guid}/tickets/{ticketId:guid}/acknowledge", async (
            Guid ticketId,
            StationAcknowledgeHandler handler,
            CancellationToken cancellationToken) =>
        {
            return await handler.AcknowledgeAsync(ticketId, cancellationToken);
        });

        return routes;
    }
}

public sealed class StationShellResponder
{
    private readonly IWebHostEnvironment environment;

    public StationShellResponder(IWebHostEnvironment environment)
    {
        this.environment = environment;
    }

    public IResult Respond()
    {
        string shellPath = Path.Combine(environment.WebRootPath ?? string.Empty, "index.html");

        return File.Exists(shellPath)
            ? Results.File(shellPath, "text/html")
            : Results.NotFound();
    }
}

public sealed class ClientRouteFallbackResponder
{
    private const string ApiPrefix = "/api";
    private const string HubPrefix = "/hub";

    private readonly StationShellResponder shellResponder;

    public ClientRouteFallbackResponder(StationShellResponder shellResponder)
    {
        this.shellResponder = shellResponder;
    }

    public IResult Respond(HttpContext httpContext)
    {
        if (httpContext.Request.Path.StartsWithSegments(ApiPrefix)
            || httpContext.Request.Path.StartsWithSegments(HubPrefix))
        {
            return Results.NotFound();
        }

        return shellResponder.Respond();
    }
}

public sealed record StationPrintabilityRow(Guid LocationId, StationPrintability Printability);

public sealed class StationPrintabilityReader
{
    public async Task<IReadOnlyDictionary<Guid, StationPrintability>> ReadAsync(
        GastronomyAppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        List<PrinterStatus> statuses = await dbContext.PrinterStatuses.AsNoTracking().ToListAsync(cancellationToken);
        List<PrinterConfiguration> configurations = await dbContext.PrinterConfigurations
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        Dictionary<Guid, StationPrintability> printability = [];

        foreach (PrinterConfiguration configuration in configurations)
        {
            PrinterStatus? status = statuses.FirstOrDefault(
                candidate => candidate.ProductionLocationId == configuration.ProductionLocationId);

            printability[configuration.ProductionLocationId] = new StationPrintability
            {
                IsFaulty = status?.IsFaulty ?? true,
                IsOnline = status?.IsOnline ?? false,
                IsPaperEnd = status?.IsPaperEnd ?? false,
                IsCoverOpen = status?.IsCoverOpen ?? false,
                IsInErrorState = status?.IsInErrorState ?? false,
                IsEnabled = configuration.IsEnabled,
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
