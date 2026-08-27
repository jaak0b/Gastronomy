using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.Printing;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Printing;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Hub;

public sealed class HubNotificationDispatcher : IPrintCallbacks
{
    private readonly IHubContext<GastronomyHub> hubContext;
    private readonly IDbContextFactory<GastronomyAppDbContext> contextFactory;
    private readonly TicketFailureMessages failureMessages = new();
    private readonly HubGroupNames groupNames = new();
    private readonly HubEventNames eventNames = new();

    public HubNotificationDispatcher(
        IHubContext<GastronomyHub> hubContext,
        IDbContextFactory<GastronomyAppDbContext> contextFactory)
    {
        this.hubContext = hubContext;
        this.contextFactory = contextFactory;
    }

    public async Task OnTicketStatusChangedAsync(
        Guid orderId,
        Guid locationTicketId,
        LocationTicketStatus newStatus,
        PrintFailureReason? failureReason,
        CancellationToken ct)
    {
        await using GastronomyAppDbContext context = await contextFactory.CreateDbContextAsync(ct);

        Order? order = await context.Orders.FirstOrDefaultAsync(candidate => candidate.Id == orderId, ct);
        LocationTicket? ticket = await context.LocationTickets
            .FirstOrDefaultAsync(candidate => candidate.Id == locationTicketId, ct);

        if (order is null || ticket is null)
        {
            return;
        }

        ProductionLocation? location = await context.ProductionLocations
            .FirstOrDefaultAsync(candidate => candidate.Id == ticket.ProductionLocationId, ct);

        PrinterStatus? printerStatus = await context.PrinterStatuses
            .FirstOrDefaultAsync(candidate => candidate.ProductionLocationId == ticket.ProductionLocationId, ct);

        TicketMessage message = failureMessages.Describe(newStatus, failureReason, location?.Name ?? string.Empty);

        TicketStatusChangedEvent payload = new(
            order.Id,
            order.GlobalOrderNumber,
            ticket.Id,
            ticket.ProductionLocationId,
            location?.Name ?? string.Empty,
            ticket.LocationSequenceNumber,
            newStatus.ToString(),
            failureReason?.ToString(),
            printerStatus is not null && !printerStatus.IsPaperEnd,
            message.MessageKey,
            message.Parameters);

        await SendToAsync(
            eventNames.TicketStatusChanged,
            payload,
            [groupNames.Person(order.ServerPersonId), groupNames.Admin, groupNames.Stations],
            ct);
    }

    public async Task OnOrderStatusChangedAsync(Guid orderId, OrderStatus newStatus, CancellationToken ct)
    {
        await using GastronomyAppDbContext context = await contextFactory.CreateDbContextAsync(ct);
        Order? order = await context.Orders.FirstOrDefaultAsync(candidate => candidate.Id == orderId, ct);

        if (order is null)
        {
            return;
        }

        await SendToAsync(
            eventNames.OrderStatusChanged,
            new OrderStatusChangedEvent(orderId, newStatus.ToString()),
            [groupNames.Person(order.ServerPersonId), groupNames.Admin],
            ct);
    }

    public async Task OnPrinterStatusChangedAsync(
        Guid productionLocationId,
        PrinterStatusSnapshot snapshot,
        bool isFaulty,
        int waitingTicketCount,
        CancellationToken ct)
    {
        await using GastronomyAppDbContext context = await contextFactory.CreateDbContextAsync(ct);
        ProductionLocation? location = await context.ProductionLocations
            .FirstOrDefaultAsync(candidate => candidate.Id == productionLocationId, ct);

        PrinterStatusChangedEvent payload = new(
            productionLocationId,
            location?.Name ?? string.Empty,
            snapshot.IsOnline,
            snapshot.IsPaperEnd,
            snapshot.IsPaperNearEnd,
            snapshot.IsCoverOpen,
            isFaulty,
            waitingTicketCount,
            snapshot.Detail);

        await SendToAsync(
            eventNames.PrinterStatusChanged,
            payload,
            [groupNames.Devices, groupNames.Admin, groupNames.Stations],
            ct);
    }

    public async Task PushOrderAcceptedAsync(Guid serverPersonId, OrderAcceptedEvent payload, CancellationToken ct)
    {
        await SendToAsync(
            eventNames.OrderAccepted,
            payload,
            [groupNames.Person(serverPersonId), groupNames.Admin, groupNames.Stations],
            ct);
    }

    public async Task PushCatalogChangedAsync(string version, CancellationToken ct)
    {
        await SendToAsync(
            eventNames.CatalogChanged,
            new CatalogChangedEvent(version),
            [groupNames.Devices, groupNames.Admin],
            ct);
    }

    public async Task PushEnrolmentCompletedAsync(EnrolmentCompletedEvent payload, CancellationToken ct)
    {
        await SendToAsync(eventNames.EnrolmentCompleted, payload, [groupNames.Admin], ct);
    }

    public async Task PushDeviceRevokedAsync(Guid deviceId, CancellationToken ct)
    {
        await SendToAsync(
            eventNames.DeviceRevoked,
            new DeviceRevokedEvent(deviceId),
            [groupNames.Device(deviceId), groupNames.Admin],
            ct);
    }

    public async Task PushEventSessionStartedAsync(EventSessionStartedEvent payload, CancellationToken ct)
    {
        await SendToAsync(
            eventNames.EventSessionStarted,
            payload,
            [groupNames.Devices, groupNames.Admin, groupNames.Stations],
            ct);
    }

    private async Task SendToAsync(
        string eventName,
        object payload,
        IReadOnlyList<string> groups,
        CancellationToken ct)
    {
        foreach (string group in groups)
        {
            await hubContext.Clients.Group(group).SendAsync(eventName, payload, ct);
        }
    }
}

public sealed record TicketMessage(string? MessageKey, IReadOnlyDictionary<string, string> Parameters);

public sealed class TicketFailureMessages
{
    public TicketMessage Describe(
        LocationTicketStatus status,
        PrintFailureReason? failureReason,
        string locationName)
    {
        Dictionary<string, string> parameters = new() { ["station"] = locationName };

        if (failureReason is not null)
        {
            return new TicketMessage(KeyFor(failureReason.Value), parameters);
        }

        return status switch
        {
            LocationTicketStatus.Unknown => new TicketMessage("ticket.unknownOutcome", parameters),
            LocationTicketStatus.Blocked => new TicketMessage("ticket.blocked", parameters),
            LocationTicketStatus.Failed => new TicketMessage("ticket.failed", parameters),
            LocationTicketStatus.Queued => new TicketMessage(null, parameters),
            LocationTicketStatus.Printing => new TicketMessage(null, parameters),
            LocationTicketStatus.Printed => new TicketMessage(null, parameters),
            LocationTicketStatus.PrintedOnTestPrinter => new TicketMessage("ticket.printedOnTestPrinter", parameters),
            LocationTicketStatus.HandledOnPaper => new TicketMessage("ticket.handledOnPaper", parameters),
            _ => new Never().OfType<TicketMessage>(status),
        };
    }

    private string KeyFor(PrintFailureReason failureReason)
    {
        return failureReason switch
        {
            PrintFailureReason.PaperEnd => "ticket.paperEnd",
            PrintFailureReason.CoverOpen => "ticket.coverOpen",
            PrintFailureReason.Unreachable => "ticket.unreachable",
            PrintFailureReason.Timeout => "ticket.timeout",
            PrintFailureReason.SocketDropped => "ticket.socketDropped",
            PrintFailureReason.PrinterError => "ticket.printerError",
            PrintFailureReason.StationDisabled => "ticket.stationDisabled",
            PrintFailureReason.StationFaulty => "ticket.stationFaulty",
            PrintFailureReason.TicketResolvedByHuman => "ticket.resolvedByHuman",
            _ => new Never().OfType<string>(failureReason),
        };
    }
}
