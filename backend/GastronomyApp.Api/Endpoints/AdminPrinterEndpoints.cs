using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Printing;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Printing;
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

        group.MapPut("/{stationId:guid}", async (
            Guid stationId,
            SavePrinterRequest request,
            AdminPrinterHandler handler,
            CancellationToken cancellationToken) => await handler.SaveAsync(stationId, request, cancellationToken));

        group.MapPost("/{stationId:guid}/test-print", async (
            Guid stationId,
            AdminPrinterHandler handler,
            CancellationToken cancellationToken) => await handler.TestPrintAsync(stationId, cancellationToken));

        group.MapPost("/{stationId:guid}/reconnect", async (
            Guid stationId,
            AdminPrinterHandler handler,
            CancellationToken cancellationToken) => await handler.ReconnectAsync(stationId, cancellationToken));

        routes.MapPost("/api/admin/mock/{stationId:guid}/fault", async (
            Guid stationId,
            ArmMockFaultRequest request,
            AdminPrinterHandler handler,
            CancellationToken cancellationToken) =>
                await handler.ArmMockFaultAsync(stationId, request, cancellationToken));

        return routes;
    }
}

public sealed class AdminPrinterHandler
{
    private readonly GastronomyAppDbContext dbContext;
    private readonly IPrinterFleet printerFleet;
    private readonly PrinterFleet fleet;
    private readonly IMockFaultRegistry mockFaultRegistry;
    private readonly IPrinterWorkerDataAccess printerWorkerDataAccess;
    private readonly MockPrinterTransport mockPrinterTransport;
    private readonly ResultEnvelope resultEnvelope;

    public AdminPrinterHandler(
        GastronomyAppDbContext dbContext,
        IPrinterFleet printerFleet,
        PrinterFleet fleet,
        IMockFaultRegistry mockFaultRegistry,
        IPrinterWorkerDataAccess printerWorkerDataAccess,
        MockPrinterTransport mockPrinterTransport,
        ResultEnvelope resultEnvelope)
    {
        this.dbContext = dbContext;
        this.printerFleet = printerFleet;
        this.fleet = fleet;
        this.mockFaultRegistry = mockFaultRegistry;
        this.printerWorkerDataAccess = printerWorkerDataAccess;
        this.mockPrinterTransport = mockPrinterTransport;
        this.resultEnvelope = resultEnvelope;
    }

    public async Task<IResult> ListAsync(CancellationToken cancellationToken)
    {
        List<Station> stations = await dbContext.Stations
            .AsNoTracking()
            .OrderBy(station => station.SortOrder)
            .ToListAsync(cancellationToken);

        List<PrinterConfiguration> configurations = await dbContext.PrinterConfigurations
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        List<PrinterStatus> statuses = await dbContext.PrinterStatuses
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        List<AdminPrinterView> views = [];

        foreach (Station station in stations)
        {
            PrinterConfiguration? configuration = configurations.FirstOrDefault(
                candidate => candidate.StationId == station.Id);

            if (configuration is null)
            {
                continue;
            }

            PrinterStatus? status = statuses.FirstOrDefault(
                candidate => candidate.StationId == station.Id);

            IReadOnlyList<string> sharedWithStationNames = configurations
                .Where(candidate => candidate.StationId != station.Id
                    && candidate.TransportKind == configuration.TransportKind
                    && candidate.Host == configuration.Host
                    && candidate.Port == configuration.Port)
                .Join(
                    stations,
                    candidate => candidate.StationId,
                    sharing => sharing.Id,
                    (candidate, sharing) => sharing.Name)
                .ToList();

            int waitingTicketCount = await printerWorkerDataAccess.CountWaitingTicketsAsync(
                station.Id,
                cancellationToken);

            views.Add(new AdminPrinterView(
                station.Id,
                station.Name,
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
                status?.IsPaperNearEnd ?? false,
                status?.IsCoverOpen ?? false,
                status?.IsFaulty ?? false,
                waitingTicketCount,
                status?.LastChangedAtUtc,
                sharedWithStationNames,
                configuration.TransportKind == TransportKind.Mock
                    ? mockPrinterTransport.SlipRootFolderPath
                    : null));
        }

        return Results.Ok(new AdminPrinterListView(views));
    }

    public async Task<IResult> SaveAsync(
        Guid stationId,
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
            .FirstOrDefaultAsync(candidate => candidate.StationId == stationId, cancellationToken);

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

        List<Guid> sharingStationIds = await dbContext.PrinterConfigurations
            .AsNoTracking()
            .Where(candidate => candidate.TransportKind == transportKind
                && candidate.Host == request.Host
                && candidate.Port == request.Port)
            .Select(candidate => candidate.StationId)
            .ToListAsync(cancellationToken);

        return Results.Ok(new SharedEndpointView(stationId, sharingStationIds));
    }

    public async Task<IResult> TestPrintAsync(Guid stationId, CancellationToken cancellationToken)
    {
        if (!await StationExistsAsync(stationId, cancellationToken))
        {
            return Results.NotFound();
        }

        try
        {
            await printerFleet.TestPrintAsync(stationId, cancellationToken);
        }
        catch (UnknownLocationTicketException)
        {
            return NoWorkerServesThisStation();
        }

        return Results.Json(new SharedEndpointView(stationId, []), statusCode: StatusCodes.Status202Accepted);
    }

    public async Task<IResult> ReconnectAsync(Guid stationId, CancellationToken cancellationToken)
    {
        if (!await StationExistsAsync(stationId, cancellationToken))
        {
            return Results.NotFound();
        }

        IReadOnlyList<Guid> clearedStationIds;

        try
        {
            clearedStationIds = await printerFleet.ReconnectAsync(stationId, cancellationToken);
        }
        catch (UnknownLocationTicketException)
        {
            return NoWorkerServesThisStation();
        }

        return Results.Json(
            new ReconnectedView(stationId, clearedStationIds),
            statusCode: StatusCodes.Status202Accepted);
    }

    public async Task<IResult> ArmMockFaultAsync(
        Guid stationId,
        ArmMockFaultRequest request,
        CancellationToken cancellationToken)
    {
        PrinterConfiguration? configuration = await dbContext.PrinterConfigurations
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.StationId == stationId, cancellationToken);

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

        mockFaultRegistry.Arm(stationId, fault, mode);

        return Results.Ok(new ArmedMockFaultView(stationId, fault.ToString(), mode.ToString()));
    }

    private IResult NoWorkerServesThisStation()
    {
        return resultEnvelope.Problem(
            StatusCodes.Status409Conflict,
            "NoPrinterWorkerForStation",
            "admin.stationHasNoPrinterWorker");
    }

    private Task<bool> StationExistsAsync(Guid stationId, CancellationToken cancellationToken)
    {
        return dbContext.Stations.AnyAsync(
            station => station.Id == stationId,
            cancellationToken);
    }
}

public sealed record ArmedMockFaultView(Guid StationId, string Fault, string Mode);
