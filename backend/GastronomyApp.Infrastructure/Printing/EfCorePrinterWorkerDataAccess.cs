using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Printing;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Printing;

public sealed class EfCorePrinterWorkerDataAccess : IPrinterWorkerDataAccess
{
  private readonly Func<GastronomyAppDbContext> contextFactory;
  private readonly OrderStatusCalculator orderStatusCalculator = new();
  private readonly TimeProvider timeProvider;
  private readonly ImmediateTransactionRunner transactionRunner = new();

  public EfCorePrinterWorkerDataAccess(Func<GastronomyAppDbContext> contextFactory,
                                       TimeProvider timeProvider)
  {
    this.contextFactory = contextFactory;
    this.timeProvider = timeProvider;
  }

  public async Task<PrintJobLoadResult> LoadPrintJobAsync(Guid printJobId, CancellationToken ct)
  {
    await using var context = contextFactory();

    var job = await context.PrintJobs.AsNoTracking()
                           .SingleAsync(candidate => candidate.Id == printJobId, ct);
    var stationOrder = await context.StationOrders.AsNoTracking()
                                    .SingleAsync(candidate => candidate.Id == job.StationOrderId, ct);
    var order = await context.Orders.AsNoTracking()
                             .SingleAsync(candidate => candidate.Id == stationOrder.OrderId, ct);
    var station = await context.Stations.AsNoTracking()
                               .SingleAsync(candidate => candidate.Id == stationOrder.StationId, ct);

    List<OrderItem> items = await context.OrderItems.AsNoTracking()
                                         .Where(item => item.StationOrderId == stationOrder.Id)
                                         .ToListAsync(ct);

    List<Guid> siblingStationIds = await context.StationOrders.AsNoTracking()
                                                .Where(candidate => candidate.OrderId == stationOrder.OrderId && candidate.Id != stationOrder.Id)
                                                .Select(candidate => candidate.StationId)
                                                .Distinct()
                                                .ToListAsync(ct);

    List<string> siblingNames = await context.Stations.AsNoTracking()
                                             .Where(candidate => siblingStationIds.Contains(candidate.Id))
                                             .OrderBy(candidate => candidate.SortOrder)
                                             .Select(candidate => candidate.Name)
                                             .ToListAsync(ct);

    var staffMember = await context.StaffMembers.AsNoTracking()
                                   .SingleOrDefaultAsync(candidate => candidate.Id == order.StaffMemberId, ct);

    return new()
           {
             PrintJobId = job.Id,
             StationOrderId = stationOrder.Id,
             OrderId = order.Id,
             StationId = stationOrder.StationId,
             CopyNumber = job.CopyNumber,
             CreatedAtUtc = job.CreatedAtUtc,
             Status = job.Status,
             StationOrderNumber = stationOrder.StationOrderNumber,
             StationName = station.Name,
             GlobalOrderNumber = order.GlobalOrderNumber,
             TableName = order.TableName,
             StaffMemberName = staffMember?.Name ?? string.Empty,
             OrderNote = order.Note,
             OrderCreatedAtUtc = order.CreatedAtUtc,
             Items = [.. items.Select(item => new PrintJobItemLoadResult(item.ItemName, item.Note))],
             AlsoGoesToStationNames = siblingNames
           };
  }

  public async Task<ClaimResult> TryClaimAsync(Guid printJobId, CancellationToken ct)
  {
    await using var context = contextFactory();

    return await transactionRunner.RunAsync(context,
                                            async transactionCancellationToken =>
                                            {
                                              var job = await context.PrintJobs
                                                                     .SingleAsync(candidate => candidate.Id == printJobId, transactionCancellationToken);
                                              var stationOrder = await context.StationOrders.AsNoTracking()
                                                                              .SingleAsync(candidate => candidate.Id == job.StationOrderId, transactionCancellationToken);

                                              var claimingStation = await context.Stations
                                                                                 .SingleAsync(candidate => candidate.Id == stationOrder.StationId, transactionCancellationToken);
                                              var status = claimingStation.PrinterId is null
                                                             ? null
                                                             : await context.PrinterStatuses
                                                                            .SingleOrDefaultAsync(candidate => candidate.PrinterId == claimingStation.PrinterId,
                                                                                                  transactionCancellationToken);

                                              if (status is not null && status.IsFaulty)
                                              {
                                                return new()
                                                       {
                                                         Value = new() { Outcome = ClaimOutcome.PrinterFaulty, PrintJobId = printJobId },
                                                         ShouldCommit = false
                                                       };
                                              }

                                              if (!IsWaiting(job.Status))
                                              {
                                                return new()
                                                       {
                                                         Value = new() { Outcome = ClaimOutcome.NoLongerWaiting, PrintJobId = printJobId },
                                                         ShouldCommit = false
                                                       };
                                              }

                                              job.Status = PrintJobStatus.Sending;
                                              await context.SaveChangesAsync(transactionCancellationToken);

                                              return new TransactionOutcome<ClaimResult>
                                                     {
                                                       Value = new() { Outcome = ClaimOutcome.Claimed, PrintJobId = printJobId },
                                                       ShouldCommit = true
                                                     };
                                            },
                                            ct);
  }

  public async Task<PrintOutcomeApplied> ApplyOutcomeAsync(PrintOutcomeApplication application, CancellationToken ct)
  {
    await using var context = contextFactory();

    return await transactionRunner.RunAsync(context,
                                            async transactionCancellationToken =>
                                            {
                                              var job = await context.PrintJobs
                                                                     .SingleAsync(candidate => candidate.Id == application.PrintJobId, transactionCancellationToken);

                                              var wasHandledOnPaper = job.Status != PrintJobStatus.Sending;

                                              if (!wasHandledOnPaper)
                                              {
                                                job.Status = application.JobStatus;
                                                job.FailureReason = application.FailureReason;
                                                job.CompletedAtUtc = IsTerminal(application.JobStatus)
                                                                       ? timeProvider.GetUtcNow().UtcDateTime
                                                                       : null;

                                                await context.SaveChangesAsync(transactionCancellationToken);
                                              }

                                              return new TransactionOutcome<PrintOutcomeApplied>
                                                     {
                                                       Value = new()
                                                               {
                                                                 PrintJobStatus = job.Status,
                                                                 WasHandledOnPaper = wasHandledOnPaper
                                                               },
                                                       ShouldCommit = true
                                                     };
                                            },
                                            ct);
  }

  public async Task<IReadOnlyList<Guid>> LoadRecoverablePrintJobIdsAsync(IReadOnlyCollection<Guid> servedStationIds,
                                                                         CancellationToken ct)
  {
    await using var context = contextFactory();
    List<Guid> ids = servedStationIds.ToList();

    return await WaitingJobsAtStations(context, ids)
                .OrderBy(job => job.CreatedAtUtc)
                .Select(job => job.Id)
                .ToListAsync(ct);
  }

  public async Task MarkSendingJobsUnknownAsync(IReadOnlyCollection<Guid> servedStationIds, CancellationToken ct)
  {
    await using var context = contextFactory();
    List<Guid> ids = servedStationIds.ToList();

    List<PrintJob> sending = await context.PrintJobs
                                          .Where(job => job.Status == PrintJobStatus.Sending
                                                        && context.StationOrders.Any(stationOrder =>
                                                                                       stationOrder.Id == job.StationOrderId && ids.Contains(stationOrder.StationId)))
                                          .ToListAsync(ct);

    foreach (var job in sending)
    {
      job.Status = PrintJobStatus.Unknown;
    }

    await context.SaveChangesAsync(ct);
  }

  public async Task<int> CountWaitingPrintJobsAsync(Guid stationId, CancellationToken ct)
  {
    await using var context = contextFactory();

    return await WaitingJobsAtStations(context, [stationId]).CountAsync(ct);
  }

  public async Task FailAllWaitingAtEndpointAsync(IReadOnlyCollection<Guid> servedStationIds,
                                                  PrintFailureReason failureReason,
                                                  CancellationToken ct)
  {
    await using var context = contextFactory();
    await using var transaction =
      await context.Database.BeginTransactionAsync(ct);

    List<Guid> ids = servedStationIds.ToList();
    var now = timeProvider.GetUtcNow().UtcDateTime;

    List<Guid> faultyPrinterIds = await context.Stations.AsNoTracking()
                                               .Where(station => ids.Contains(station.Id) && station.PrinterId != null)
                                               .Select(station => station.PrinterId!.Value)
                                               .ToListAsync(ct);
    List<PrinterStatus> statuses = await context.PrinterStatuses
                                                .Where(status => faultyPrinterIds.Contains(status.PrinterId))
                                                .ToListAsync(ct);

    foreach (var status in statuses)
    {
      status.IsFaulty = true;
      status.LastChangedAtUtc = now;
      status.LastDetail = "The station circuit breaker tripped after two consecutive unknown outcomes.";
    }

    List<PrintJob> waiting = await WaitingJobsAtStations(context, ids).ToListAsync(ct);

    foreach (var job in waiting)
    {
      job.Status = PrintJobStatus.Failed;
      job.FailureReason = failureReason;
      job.CompletedAtUtc = now;
    }

    await context.SaveChangesAsync(ct);
    await transaction.CommitAsync(ct);
  }

  public async Task ClearFaultyAtEndpointAsync(IReadOnlyCollection<Guid> servedStationIds, CancellationToken ct)
  {
    await using var context = contextFactory();
    List<Guid> ids = servedStationIds.ToList();

    List<Guid> clearedPrinterIds = await context.Stations.AsNoTracking()
                                                .Where(station => ids.Contains(station.Id) && station.PrinterId != null)
                                                .Select(station => station.PrinterId!.Value)
                                                .ToListAsync(ct);
    List<PrinterStatus> statuses = await context.PrinterStatuses
                                                .Where(status => clearedPrinterIds.Contains(status.PrinterId))
                                                .ToListAsync(ct);

    foreach (var status in statuses)
    {
      status.IsFaulty = false;
      status.LastChangedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
      status.LastDetail = "A human reconnected this printer.";
    }

    await context.SaveChangesAsync(ct);
  }

  public async Task<int> AllocatePrinterJobIdAsync(CancellationToken ct)
  {
    await using var context = contextFactory();
    SequenceNumberAllocator allocator = new(context);
    return await allocator.AllocatePrinterJobIdAsync(ct);
  }

  public async Task<IReadOnlyList<Guid>> OrderQueueAsync(IReadOnlyCollection<Guid> printJobIds, CancellationToken ct)
  {
    await using var context = contextFactory();
    List<Guid> ids = printJobIds.ToList();

    return await context.PrintJobs.AsNoTracking()
                        .Where(job => ids.Contains(job.Id))
                        .OrderBy(job => job.CreatedAtUtc)
                        .Select(job => job.Id)
                        .ToListAsync(ct);
  }

  public async Task<OrderPrintJobStatuses> LoadOrderPrintJobStatusesAsync(Guid orderId, CancellationToken ct)
  {
    await using var context = contextFactory();

    List<PrintJobStatus> statuses = await LatestStatusesForOrderAsync(context, orderId, ct);

    return new()
           {
             CurrentStatus = orderStatusCalculator.Calculate(statuses),
             PrintJobStatuses = statuses
           };
  }

  public async Task WritePrinterStatusAsync(Guid printerId, PrinterStatusSnapshot snapshot, CancellationToken ct)
  {
    await using var context = contextFactory();
    var now = timeProvider.GetUtcNow().UtcDateTime;

    var status = await context.PrinterStatuses
                              .SingleOrDefaultAsync(candidate => candidate.PrinterId == printerId, ct);

    if (status is null)
    {
      context.PrinterStatuses.Add(new()
                                  {
                                    PrinterId = printerId,
                                    IsOnline = snapshot.IsOnline,
                                    IsPaperEnd = snapshot.IsPaperEnd,
                                    IsPaperNearEnd = snapshot.IsPaperNearEnd,
                                    IsCoverOpen = snapshot.IsCoverOpen,
                                    IsInErrorState = snapshot.IsInErrorState,
                                    IsFaulty = false,
                                    LastDetail = snapshot.Detail,
                                    LastChangedAtUtc = now,
                                    LastHeardFromAtUtc = now
                                  });
    }
    else
    {
      var changed = status.IsOnline != snapshot.IsOnline
                    || status.IsPaperEnd != snapshot.IsPaperEnd
                    || status.IsPaperNearEnd != snapshot.IsPaperNearEnd
                    || status.IsCoverOpen != snapshot.IsCoverOpen
                    || status.IsInErrorState != snapshot.IsInErrorState;

      status.IsOnline = snapshot.IsOnline;
      status.IsPaperEnd = snapshot.IsPaperEnd;
      status.IsPaperNearEnd = snapshot.IsPaperNearEnd;
      status.IsCoverOpen = snapshot.IsCoverOpen;
      status.IsInErrorState = snapshot.IsInErrorState;
      status.LastDetail = snapshot.Detail;
      status.LastHeardFromAtUtc = now;

      if (changed)
      {
        status.LastChangedAtUtc = now;
      }
    }

    await context.SaveChangesAsync(ct);
  }

  public async Task<IReadOnlyList<SuspensionPeriod>> LoadSuspensionPeriodsAsync(Guid stationId,
                                                                                CancellationToken ct)
  {
    await using var context = contextFactory();

    var station = await context.Stations.AsNoTracking()
                               .SingleOrDefaultAsync(candidate => candidate.Id == stationId, ct);
    var status = station?.PrinterId is null
                   ? null
                   : await context.PrinterStatuses.AsNoTracking()
                                  .SingleOrDefaultAsync(candidate => candidate.PrinterId == station.PrinterId, ct);

    var stationHasNoPrinter = station is not null && station.PrinterId is null;
    var printerInErrorState = status is not null && status.IsInErrorState;
    var mechanicalHold = status is not null && (status.IsPaperEnd || status.IsCoverOpen);

    if (!stationHasNoPrinter && !printerInErrorState && !mechanicalHold)
    {
      return [];
    }

    var startedAtUtc = status?.LastChangedAtUtc ?? DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
    return [new() { StartedAtUtc = startedAtUtc, EndedAtUtc = null }];
  }

  public async Task FailPrintJobAsync(Guid printJobId, PrintFailureReason failureReason, CancellationToken ct)
  {
    await using var context = contextFactory();
    var now = timeProvider.GetUtcNow().UtcDateTime;

    var job = await context.PrintJobs.SingleOrDefaultAsync(candidate => candidate.Id == printJobId, ct);
    if (job is null || IsTerminal(job.Status))
    {
      return;
    }

    job.Status = PrintJobStatus.Failed;
    job.FailureReason = failureReason;
    job.CompletedAtUtc = now;
    await context.SaveChangesAsync(ct);
  }

  public async Task<PrintJobEnsured> EnsureNextCopyAsync(Guid stationOrderId, CancellationToken ct)
  {
    await using var context = contextFactory();

    return await transactionRunner.RunAsync(context,
                                            async transactionCancellationToken =>
                                            {
                                              var stationId = await context.StationOrders.AsNoTracking()
                                                                           .Where(stationOrder => stationOrder.Id == stationOrderId)
                                                                           .Select(stationOrder => stationOrder.StationId)
                                                                           .SingleAsync(transactionCancellationToken);

                                              var openJob = await context.PrintJobs
                                                                         .FirstOrDefaultAsync(job => job.StationOrderId == stationOrderId
                                                                                                     && (job.Status == PrintJobStatus.Queued
                                                                                                         || job.Status == PrintJobStatus.Blocked
                                                                                                         || job.Status == PrintJobStatus.Sending),
                                                                                              transactionCancellationToken);

                                              if (openJob is not null)
                                              {
                                                return new()
                                                       {
                                                         Value = new(false, openJob.Id, stationId),
                                                         ShouldCommit = false
                                                       };
                                              }

                                              var highestCopyNumber = await context.PrintJobs
                                                                                   .Where(job => job.StationOrderId == stationOrderId)
                                                                                   .MaxAsync(job => (int?)job.CopyNumber, transactionCancellationToken)
                                                                      ?? -1;

                                              var jobId = Guid.NewGuid();

                                              context.PrintJobs.Add(new()
                                                                    {
                                                                      Id = jobId,
                                                                      StationOrderId = stationOrderId,
                                                                      CopyNumber = highestCopyNumber + 1,
                                                                      Status = PrintJobStatus.Queued,
                                                                      PrinterJobId = null,
                                                                      FailureReason = null,
                                                                      CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime
                                                                    });

                                              await context.SaveChangesAsync(transactionCancellationToken);

                                              return new TransactionOutcome<PrintJobEnsured>
                                                     {
                                                       Value = new(true, jobId, stationId),
                                                       ShouldCommit = true
                                                     };
                                            },
                                            ct);
  }

  public async Task<Guid?> ResolveStationAsync(Guid printJobId, CancellationToken ct)
  {
    await using var context = contextFactory();

    return await context.PrintJobs.AsNoTracking()
                        .Where(job => job.Id == printJobId)
                        .Join(context.StationOrders.AsNoTracking(),
                              job => job.StationOrderId,
                              stationOrder => stationOrder.Id,
                              (job, stationOrder) => (Guid?)stationOrder.StationId)
                        .SingleOrDefaultAsync(ct);
  }

  private static IQueryable<PrintJob> WaitingJobsAtStations(GastronomyAppDbContext context,
                                                            IReadOnlyCollection<Guid> stationIds)
  {
    List<Guid> ids = stationIds.ToList();

    return context.PrintJobs
                  .Where(job => (job.Status == PrintJobStatus.Queued || job.Status == PrintJobStatus.Blocked)
                                && context.StationOrders.Any(stationOrder =>
                                                               stationOrder.Id == job.StationOrderId && ids.Contains(stationOrder.StationId)));
  }

  private async static Task<List<PrintJobStatus>> LatestStatusesForOrderAsync(GastronomyAppDbContext context,
                                                                              Guid orderId,
                                                                              CancellationToken ct)
  {
    List<Guid> stationOrderIds = await context.StationOrders.AsNoTracking()
                                              .Where(stationOrder => stationOrder.OrderId == orderId)
                                              .Select(stationOrder => stationOrder.Id)
                                              .ToListAsync(ct);

    List<PrintJobStatus> statuses = [];

    foreach (var stationOrderId in stationOrderIds)
    {
      var status = await context.PrintJobs.AsNoTracking()
                                .Where(job => job.StationOrderId == stationOrderId)
                                .OrderByDescending(job => job.CopyNumber)
                                .Select(job => job.Status)
                                .FirstAsync(ct);

      statuses.Add(status);
    }

    return statuses;
  }

  private static bool IsWaiting(PrintJobStatus status)
  {
    return status is PrintJobStatus.Queued or PrintJobStatus.Blocked;
  }

  private static bool IsTerminal(PrintJobStatus status)
  {
    return status switch
           {
             PrintJobStatus.Printed => true,
             PrintJobStatus.Failed => true,
             PrintJobStatus.Unknown => true,
             PrintJobStatus.HandledOnPaper => true,
             PrintJobStatus.Queued => false,
             PrintJobStatus.Blocked => false,
             PrintJobStatus.Sending => false,
             _ => new Never().OfType<bool>(status)
           };
  }
}
