using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.Printing;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Printing;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Hub;

public sealed class HubNotificationDispatcher : IPrintCallbacks
{
  private readonly IDbContextFactory<GastronomyAppDbContext> _contextFactory;
  private readonly HubEventNames _eventNames = new();
  private readonly PrintFailureMessages _failureMessages = new();
  private readonly HubGroupNames _groupNames = new();
  private readonly IHubContext<GastronomyHub> _hubContext;

  public HubNotificationDispatcher(IHubContext<GastronomyHub> hubContext,
                                   IDbContextFactory<GastronomyAppDbContext> contextFactory)
  {
    _hubContext = hubContext;
    _contextFactory = contextFactory;
  }

  public async Task OnPrintJobStatusChangedAsync(Guid orderId,
                                                 Guid stationOrderId,
                                                 PrintJobStatus newStatus,
                                                 PrintFailureReason? failureReason,
                                                 CancellationToken ct)
  {
    await using var context = await _contextFactory.CreateDbContextAsync(ct);

    var order = await context.Orders.FirstOrDefaultAsync(candidate => candidate.Id == orderId, ct);
    var stationOrder = await context.StationOrders
                                    .FirstOrDefaultAsync(candidate => candidate.Id == stationOrderId, ct);

    if (order is null || stationOrder is null)
    {
      return;
    }

    var station = await context.Stations
                               .FirstOrDefaultAsync(candidate => candidate.Id == stationOrder.StationId, ct);

    var printerStatus = station?.PrinterId is null
                          ? null
                          : await context.PrinterStatuses
                                         .FirstOrDefaultAsync(candidate => candidate.PrinterId == station.PrinterId, ct);

    var message = _failureMessages.Describe(newStatus, failureReason, station?.Name ?? string.Empty);

    PrintJobStatusChangedEvent payload = new(order.Id,
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

    await SendToAsync(_eventNames.PrintJobStatusChanged,
                      payload,
                      [_groupNames.Devices, _groupNames.Admin],
                      ct);
  }

  public async Task OnOrderStatusChangedAsync(Guid orderId, OrderStatus newStatus, CancellationToken ct)
  {
    await using var context = await _contextFactory.CreateDbContextAsync(ct);
    var order = await context.Orders.FirstOrDefaultAsync(candidate => candidate.Id == orderId, ct);

    if (order is null)
    {
      return;
    }

    await SendToAsync(_eventNames.OrderStatusChanged,
                      new OrderStatusChangedEvent(orderId, newStatus.ToString()),
                      [_groupNames.StaffMember(order.StaffMemberId), _groupNames.Admin],
                      ct);
  }

  public async Task OnPrinterStatusChangedAsync(Guid printerId,
                                                IReadOnlyList<Guid> stationIds,
                                                PrinterStatusSnapshot snapshot,
                                                bool isFaulty,
                                                int waitingPrintJobCount,
                                                CancellationToken ct)
  {
    ArgumentNullException.ThrowIfNull(stationIds);

    await using var context = await _contextFactory.CreateDbContextAsync(ct);

    foreach (var stationId in stationIds)
    {
      var station = await context.Stations
                                 .FirstOrDefaultAsync(candidate => candidate.Id == stationId, ct);

      PrinterStatusChangedEvent payload = new(stationId,
                                              station?.Name ?? string.Empty,
                                              snapshot.IsOnline,
                                              snapshot.IsPaperEnd,
                                              snapshot.IsPaperNearEnd,
                                              snapshot.IsCoverOpen,
                                              isFaulty,
                                              waitingPrintJobCount,
                                              snapshot.ObservedAt.UtcDateTime,
                                              snapshot.Detail);

      await SendToAsync(_eventNames.PrinterStatusChanged,
                        payload,
                        [_groupNames.Devices, _groupNames.Admin],
                        ct);
    }
  }

  public async Task PushOrderAcceptedAsync(Guid staffMemberId, OrderAcceptedEvent payload, CancellationToken ct)
  {
    await SendToAsync(_eventNames.OrderAccepted,
                      payload,
                      [_groupNames.StaffMember(staffMemberId), _groupNames.Admin],
                      ct);

    foreach (var stationId in payload.StationOrders.Select(stationOrder => stationOrder.StationId).Distinct())
    {
      await SendToAsync(_eventNames.StationBacklogChanged,
                        new StationBacklogChangedEvent(stationId),
                        [_groupNames.Devices, _groupNames.Admin],
                        ct);
    }
  }

  public async Task PushCatalogChangedAsync(string version, CancellationToken ct)
  {
    await SendToAsync(_eventNames.CatalogChanged,
                      new CatalogChangedEvent(version),
                      [_groupNames.Devices, _groupNames.Admin],
                      ct);
  }

  public async Task PushEnrolmentCompletedAsync(EnrolmentCompletedEvent payload, CancellationToken ct)
  {
    await SendToAsync(_eventNames.EnrolmentCompleted, payload, [_groupNames.Admin], ct);
  }

  public async Task PushDeviceRevokedAsync(Guid deviceId, CancellationToken ct)
  {
    await SendToAsync(_eventNames.DeviceRevoked,
                      new DeviceRevokedEvent(deviceId),
                      [_groupNames.Device(deviceId), _groupNames.Admin],
                      ct);
  }

  private async Task SendToAsync(string eventName,
                                 object payload,
                                 IReadOnlyList<string> groups,
                                 CancellationToken ct)
  {
    foreach (var group in groups)
    {
      await _hubContext.Clients.Group(group).SendAsync(eventName, payload, ct);
    }
  }
}

public sealed record PrintMessage(string? MessageKey, IReadOnlyDictionary<string, string> Parameters);

public sealed class PrintFailureMessages
{
  public PrintMessage Describe(PrintJobStatus status,
                               PrintFailureReason? failureReason,
                               string stationName)
  {
    Dictionary<string, string> parameters = new() { ["station"] = stationName };

    if (failureReason is not null)
    {
      return new(KeyFor(failureReason.Value), parameters);
    }

    return status switch
           {
             PrintJobStatus.Unknown => new("printJob.unknownOutcome", parameters),
             PrintJobStatus.Blocked => new("printJob.blocked", parameters),
             PrintJobStatus.Failed => new("printJob.failed", parameters),
             PrintJobStatus.Queued => new(null, parameters),
             PrintJobStatus.Sending => new(null, parameters),
             PrintJobStatus.Printed => new(null, parameters),
             PrintJobStatus.HandledOnPaper => new("printJob.handledOnPaper", parameters),
             _ => new Never().OfType<PrintMessage>(status)
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
             _ => new Never().OfType<string>(failureReason)
           };
  }
}
