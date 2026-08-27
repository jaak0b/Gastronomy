using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Printing;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Infrastructure;
using GastronomyApp.Infrastructure.Printing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Endpoints;

public static class AdminPrinterEndpoints
{
    public static IEndpointRouteBuilder MapAdminPrinterEndpoints(this IEndpointRouteBuilder routes)
    {
        RouteGroupBuilder group = routes.MapGroup("/api/admin/printers");

        group.MapGet(string.Empty, async (
            AdminPrinterHandler handler,
            CancellationToken cancellationToken) => await handler.ListAsync(cancellationToken));

        group.MapPut("/{locationId:guid}", async (
            Guid locationId,
            SavePrinterRequest request,
            AdminPrinterHandler handler,
            CancellationToken cancellationToken) => await handler.SaveAsync(locationId, request, cancellationToken));

        group.MapPost("/{locationId:guid}/test-print", async (
            Guid locationId,
            AdminPrinterHandler handler,
            CancellationToken cancellationToken) => await handler.TestPrintAsync(locationId, cancellationToken));

        group.MapPost("/{locationId:guid}/reconnect", async (
            Guid locationId,
            AdminPrinterHandler handler,
            CancellationToken cancellationToken) => await handler.ReconnectAsync(locationId, cancellationToken));

        routes.MapPost("/api/admin/mock/{locationId:guid}/fault", async (
            Guid locationId,
            ArmMockFaultRequest request,
            AdminPrinterHandler handler,
            CancellationToken cancellationToken) =>
                await handler.ArmMockFaultAsync(locationId, request, cancellationToken));

        return routes;
    }
}

public sealed class AdminPrinterHandler
{
    private readonly GastronomyAppDbContext dbContext;
    private readonly IPrinterFleet printerFleet;
    private readonly PrinterFleet fleet;
    private readonly IMockFaultRegistry mockFaultRegistry;
    private readonly ResultEnvelope resultEnvelope;

    public AdminPrinterHandler(
        GastronomyAppDbContext dbContext,
        IPrinterFleet printerFleet,
        PrinterFleet fleet,
        IMockFaultRegistry mockFaultRegistry,
        ResultEnvelope resultEnvelope)
    {
        this.dbContext = dbContext;
        this.printerFleet = printerFleet;
        this.fleet = fleet;
        this.mockFaultRegistry = mockFaultRegistry;
        this.resultEnvelope = resultEnvelope;
    }

    public async Task<IResult> ListAsync(CancellationToken cancellationToken)
    {
        List<ProductionLocation> locations = await dbContext.ProductionLocations
            .AsNoTracking()
            .OrderBy(location => location.SortOrder)
            .ToListAsync(cancellationToken);

        List<PrinterConfiguration> configurations = await dbContext.PrinterConfigurations
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        List<PrinterStatus> statuses = await dbContext.PrinterStatuses
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        List<AdminPrinterView> views = [];

        foreach (ProductionLocation location in locations)
        {
            PrinterConfiguration? configuration = configurations.FirstOrDefault(
                candidate => candidate.ProductionLocationId == location.Id);

            if (configuration is null)
            {
                continue;
            }

            PrinterStatus? status = statuses.FirstOrDefault(
                candidate => candidate.ProductionLocationId == location.Id);

            views.Add(new AdminPrinterView(
                location.Id,
                location.Name,
                configuration.TransportKind.ToString(),
                configuration.Host,
                configuration.Port,
                configuration.AgentIdentifier,
                configuration.CharactersPerLine,
                configuration.CodePageName,
                configuration.ConnectTimeoutSeconds,
                configuration.JobTimeoutSeconds,
                configuration.HeartbeatSeconds,
                configuration.IsEnabled,
                status?.IsOnline ?? false,
                status?.IsPaperEnd ?? false,
                status?.IsCoverOpen ?? false,
                status?.IsFaulty ?? false));
        }

        return Results.Ok(new AdminPrinterListView(views));
    }

    public async Task<IResult> SaveAsync(
        Guid locationId,
        SavePrinterRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse(request.TransportKind, out TransportKind transportKind))
        {
            return resultEnvelope.Problem(
                StatusCodes.Status400BadRequest,
                "ValidationFailed",
                "admin.unknownTransportKind");
        }

        PrinterConfiguration? configuration = await dbContext.PrinterConfigurations
            .FirstOrDefaultAsync(candidate => candidate.ProductionLocationId == locationId, cancellationToken);

        if (configuration is null)
        {
            return Results.NotFound();
        }

        configuration.TransportKind = transportKind;
        configuration.Host = request.Host;
        configuration.Port = request.Port;
        configuration.AgentIdentifier = request.AgentIdentifier;
        configuration.CharactersPerLine = request.CharactersPerLine;
        configuration.CodePageName = request.CodePageName ?? configuration.CodePageName;
        configuration.ConnectTimeoutSeconds = request.ConnectTimeoutSeconds;
        configuration.JobTimeoutSeconds = request.JobTimeoutSeconds;
        configuration.HeartbeatSeconds = request.HeartbeatSeconds;
        configuration.IsEnabled = request.IsEnabled;

        await dbContext.SaveChangesAsync(cancellationToken);
        await fleet.ReconcileAsync(cancellationToken);

        List<Guid> sharingLocationIds = await dbContext.PrinterConfigurations
            .AsNoTracking()
            .Where(candidate => candidate.TransportKind == transportKind
                && candidate.Host == request.Host
                && candidate.Port == request.Port)
            .Select(candidate => candidate.ProductionLocationId)
            .ToListAsync(cancellationToken);

        return Results.Ok(new SharedEndpointView(locationId, sharingLocationIds));
    }

    public async Task<IResult> TestPrintAsync(Guid locationId, CancellationToken cancellationToken)
    {
        if (!await LocationExistsAsync(locationId, cancellationToken))
        {
            return Results.NotFound();
        }

        try
        {
            await printerFleet.TestPrintAsync(locationId, cancellationToken);
        }
        catch (UnknownLocationTicketException)
        {
            return NoWorkerServesThisStation();
        }

        return Results.Json(new SharedEndpointView(locationId, []), statusCode: StatusCodes.Status202Accepted);
    }

    public async Task<IResult> ReconnectAsync(Guid locationId, CancellationToken cancellationToken)
    {
        if (!await LocationExistsAsync(locationId, cancellationToken))
        {
            return Results.NotFound();
        }

        IReadOnlyList<Guid> clearedLocationIds;

        try
        {
            clearedLocationIds = await printerFleet.ReconnectAsync(locationId, cancellationToken);
        }
        catch (UnknownLocationTicketException)
        {
            return NoWorkerServesThisStation();
        }

        return Results.Json(
            new ReconnectedView(locationId, clearedLocationIds),
            statusCode: StatusCodes.Status202Accepted);
    }

    public async Task<IResult> ArmMockFaultAsync(
        Guid locationId,
        ArmMockFaultRequest request,
        CancellationToken cancellationToken)
    {
        PrinterConfiguration? configuration = await dbContext.PrinterConfigurations
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.ProductionLocationId == locationId, cancellationToken);

        if (configuration is null)
        {
            return Results.NotFound();
        }

        if (configuration.TransportKind != TransportKind.Mock)
        {
            return resultEnvelope.Problem(
                StatusCodes.Status422UnprocessableEntity,
                "UnprocessableEntity",
                "admin.stationIsNotOnTheTestPrinter");
        }

        if (!Enum.TryParse(request.Fault, out MockFault fault) || !Enum.TryParse(request.Mode, out MockFaultMode mode))
        {
            return resultEnvelope.Problem(
                StatusCodes.Status400BadRequest,
                "ValidationFailed",
                "admin.unknownMockFault");
        }

        mockFaultRegistry.Arm(locationId, fault, mode);

        return Results.Ok(new ArmedMockFaultView(locationId, fault.ToString(), mode.ToString()));
    }

    private IResult NoWorkerServesThisStation()
    {
        return resultEnvelope.Problem(
            StatusCodes.Status409Conflict,
            "NoPrinterWorkerForStation",
            "admin.stationHasNoPrinterWorker");
    }

    private Task<bool> LocationExistsAsync(Guid locationId, CancellationToken cancellationToken)
    {
        return dbContext.ProductionLocations.AnyAsync(
            location => location.Id == locationId,
            cancellationToken);
    }
}

public sealed record ArmedMockFaultView(Guid LocationId, string Fault, string Mode);
