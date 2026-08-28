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
  private readonly PrintFailureMessages failureMessages = new();
  private readonly HubGroupNames groupNames = new();
  private readonly HubEventNames eventNames = new();

  public HubNotificationDispatcher(
      IHubContext<GastronomyHub> hubContext,
      IDbContextFactory<GastronomyAppDbContext> contextFactory)
  {
    this.hubContext = hubContext;
    this.contextFactory = contextFactory;
  }

  public async Task OnPrintJobStatusChangedAsync(
      Guid orderId,
      Guid stationOrderId,
      PrintJobStatus newStatus,
      PrintFailureReason? failureReason,
      CancellationToken ct)
  {
    await using GastronomyAppDbContext context = await contextFactory.CreateDbContextAsync(ct);

    Order? order = await context.Orders.FirstOrDefaultAsync(candidate => candidate.Id == orderId, ct);
    StationOrder? stationOrder = await context.StationOrders
        .FirstOrDefaultAsync(candidate => candidate.Id == stationOrderId, ct);

    if (order is null || stationOrder is null)
    {
      return;
    }

    Station? station = await context.Stations
        .FirstOrDefaultAsync(candidate => candidate.Id == stationOrder.StationId, ct);

    PrinterStatus? printerStatus = station?.PrinterId is null
        ? null
        : await context.PrinterStatuses
            .FirstOrDefaultAsync(candidate => candidate.PrinterId == station.PrinterId, ct);

    PrintMessage message = failureMessages.Describe(newStatus, failureReason, station?.Name ?? string.Empty);

    PrintJobStatusChangedEvent payload = new(
        order.Id,
        order.GlobalOrderNumber,
        stationOrder.Id,
        stationOrder.StationId,
        station?.Name ?? string.Empty,
        stationOrder.StationOrderNumber,
        newStatus.ToString(),
        failureReason?.ToString(),
        printerStatus is not null && !printerStatus.IsPaperEnd,
        message.MessageKey,
        message.Parameters);

    await SendToAsync(
        eventNames.PrintJobStatusChanged,
        payload,
        [groupNames.Devices, groupNames.Admin],
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
        [groupNames.StaffMember(order.StaffMemberId), groupNames.Admin],
        ct);
  }

  public async Task OnPrinterStatusChangedAsync(
      Guid printerId,
      IReadOnlyList<Guid> stationIds,
      PrinterStatusSnapshot snapshot,
      bool isFaulty,
      int waitingPrintJobCount,
      CancellationToken ct)
  {
    ArgumentNullException.ThrowIfNull(stationIds);

    await using GastronomyAppDbContext context = await contextFactory.CreateDbContextAsync(ct);

    foreach (Guid stationId in stationIds)
    {
      Station? station = await context.Stations
          .FirstOrDefaultAsync(candidate => candidate.Id == stationId, ct);

      PrinterStatusChangedEvent payload = new(
          stationId,
          station?.Name ?? string.Empty,
          snapshot.IsOnline,
          snapshot.IsPaperEnd,
          snapshot.IsPaperNearEnd,
          snapshot.IsCoverOpen,
          isFaulty,
          waitingPrintJobCount,
          snapshot.ObservedAt.UtcDateTime,
          snapshot.Detail);

      await SendToAsync(
          eventNames.PrinterStatusChanged,
          payload,
          [groupNames.Devices, groupNames.Admin],
          ct);
    }
  }

  public async Task PushOrderAcceptedAsync(Guid staffMemberId, OrderAcceptedEvent payload, CancellationToken ct)
  {
    await SendToAsync(
        eventNames.OrderAccepted,
        payload,
        [groupNames.StaffMember(staffMemberId), groupNames.Admin],
        ct);

    foreach (Guid stationId in payload.StationOrders.Select(stationOrder => stationOrder.StationId).Distinct())
    {
      await SendToAsync(
          eventNames.StationBacklogChanged,
          new StationBacklogChangedEvent(stationId),
          [groupNames.Devices, groupNames.Admin],
          ct);
    }
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

public sealed record PrintMessage(string? MessageKey, IReadOnlyDictionary<string, string> Parameters);

public sealed class PrintFailureMessages
{
  public PrintMessage Describe(
      PrintJobStatus status,
      PrintFailureReason? failureReason,
      string stationName)
  {
    Dictionary<string, string> parameters = new() { ["station"] = stationName };

    if (failureReason is not null)
    {
      return new PrintMessage(KeyFor(failureReason.Value), parameters);
    }

    return status switch
    {
      PrintJobStatus.Unknown => new PrintMessage("printJob.unknownOutcome", parameters),
      PrintJobStatus.Blocked => new PrintMessage("printJob.blocked", parameters),
      PrintJobStatus.Failed => new PrintMessage("printJob.failed", parameters),
      PrintJobStatus.Queued => new PrintMessage(null, parameters),
      PrintJobStatus.Sending => new PrintMessage(null, parameters),
      PrintJobStatus.Printed => new PrintMessage(null, parameters),
      PrintJobStatus.HandledOnPaper => new PrintMessage("printJob.handledOnPaper", parameters),
      _ => new Never().OfType<PrintMessage>(status),
    };
  }

  private string KeyFor(PrintFailureReason failureReason)
  {
    return failureReason switch
    {
      PrintFailureReason.PaperEnd => "printJob.paperEnd",
      PrintFailureReason.CoverOpen => "printJob.coverOpen",
      PrintFailureReason.Unreachable => "printJob.unreachable",
      PrintFailureReason.Timeout => "printJob.timeout",
      PrintFailureReason.SocketDropped => "printJob.socketDropped",
      PrintFailureReason.PrinterError => "printJob.printerError",
      PrintFailureReason.StationDisabled => "printJob.stationDisabled",
      PrintFailureReason.StationFaulty => "printJob.stationFaulty",
      PrintFailureReason.HandledOnPaper => "printJob.handledOnPaper",
      _ => new Never().OfType<string>(failureReason),
    };
  }
}
