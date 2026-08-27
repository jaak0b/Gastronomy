using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Printing;
using GastronomyApp.Core.Results;
using GastronomyApp.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Printing;

public sealed class EfCorePrinterWorkerDataAccess : IPrinterWorkerDataAccess
{
    private readonly ImmediateTransactionRunner transactionRunner = new();
    private readonly Func<GastronomyAppDbContext> contextFactory;
    private readonly TimeProvider timeProvider;

    public EfCorePrinterWorkerDataAccess(
        Func<GastronomyAppDbContext> contextFactory,
        TimeProvider timeProvider)
    {
        this.contextFactory = contextFactory;
        this.timeProvider = timeProvider;
    }

    public async Task<TicketLoadResult> LoadTicketForPrintingAsync(Guid locationTicketId, CancellationToken ct)
    {
        await using GastronomyAppDbContext context = contextFactory();

        LocationTicket ticket = await context.LocationTickets.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == locationTicketId, ct);
        Order order = await context.Orders.AsNoTracking().SingleAsync(candidate => candidate.Id == ticket.OrderId, ct);
        Station station = await context.Stations.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == ticket.StationId, ct);

        List<OrderLine> lines = await context.OrderLines.AsNoTracking()
            .Where(line => line.LocationTicketId == locationTicketId)
            .ToListAsync(ct);

        List<Guid> siblingStationIds = await context.LocationTickets.AsNoTracking()
            .Where(candidate => candidate.OrderId == ticket.OrderId && candidate.Id != locationTicketId)
            .Select(candidate => candidate.StationId)
            .Distinct()
            .ToListAsync(ct);

        List<string> siblingNames = await context.Stations.AsNoTracking()
            .Where(candidate => siblingStationIds.Contains(candidate.Id))
            .OrderBy(candidate => candidate.SortOrder)
            .Select(candidate => candidate.Name)
            .ToListAsync(ct);

        StaffMember? staffMember = await context.StaffMembers.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == order.StaffMemberId, ct);

        Guid? chosenElsewhere = lines
            .Select(line => line.ChosenStationId)
            .FirstOrDefault(chosen => chosen is not null && chosen != ticket.StationId);

        string? chosenName = chosenElsewhere is null
            ? null
            : await context.Stations.AsNoTracking()
                .Where(candidate => candidate.Id == chosenElsewhere.Value)
                .Select(candidate => candidate.Name)
                .SingleOrDefaultAsync(ct);

        PrintJob? job = await LatestOpenJobAsync(context, locationTicketId, ct);

        return new TicketLoadResult
        {
            LocationTicketId = ticket.Id,
            OrderId = ticket.OrderId,
            StationId = ticket.StationId,
            PrintJobId = job?.Id ?? Guid.Empty,
            Kind = job?.Kind ?? PrintJobKind.Initial,
            CreatedAtUtc = ticket.CreatedAtUtc,
            Status = ticket.Status,
            ReprintCount = ticket.ReprintCount,
            StationSequenceNumber = ticket.StationSequenceNumber,
            StationName = station.Name,
            GlobalOrderNumber = order.GlobalOrderNumber,
            TableLabel = order.TableLabel,
            StaffMemberName = staffMember?.Name ?? string.Empty,
            OrderNote = order.Note,
            OrderCreatedAtUtc = order.CreatedAtUtc,
            Lines = [.. lines.Select(line => new TicketLineLoadResult(line.Quantity, line.ItemNameSnapshot, line.Note))],
            AlsoGoesToStationNames = siblingNames,
            ChosenStationNameIfDifferent = chosenName,
        };
    }

    public async Task<ClaimResult> TryClaimAsync(Guid locationTicketId, CancellationToken ct)
    {
        await using GastronomyAppDbContext context = contextFactory();

        return await transactionRunner.RunAsync(
            context,
            async transactionCancellationToken =>
            {
                LocationTicket ticket = await context.LocationTickets
                    .SingleAsync(candidate => candidate.Id == locationTicketId, transactionCancellationToken);
                PrintJob? job = await LatestOpenJobAsync(context, locationTicketId, transactionCancellationToken);
                Guid jobId = job?.Id ?? Guid.Empty;

                PrinterStatus? status = await context.PrinterStatuses
                    .SingleOrDefaultAsync(
                        candidate => candidate.StationId == ticket.StationId,
                        transactionCancellationToken);

                if (status is not null && status.IsFaulty)
                {
                    return new TransactionOutcome<ClaimResult>
                    {
                        Value = new ClaimResult { Outcome = ClaimOutcome.PrinterFaulty, PrintJobId = jobId },
                        ShouldCommit = false,
                    };
                }

                bool waiting = ticket.Status == LocationTicketStatus.Queued || ticket.Status == LocationTicketStatus.Blocked;
                if (!waiting)
                {
                    return new TransactionOutcome<ClaimResult>
                    {
                        Value = new ClaimResult { Outcome = ClaimOutcome.NoLongerWaiting, PrintJobId = jobId },
                        ShouldCommit = false,
                    };
                }

                ticket.Status = LocationTicketStatus.Printing;
                if (job is not null)
                {
                    job.Status = PrintJobStatus.Sending;
                }

                await context.SaveChangesAsync(transactionCancellationToken);

                return new TransactionOutcome<ClaimResult>
                {
                    Value = new ClaimResult { Outcome = ClaimOutcome.Claimed, PrintJobId = jobId },
                    ShouldCommit = true,
                };
            },
            ct);
    }

    public async Task RecordAttemptAsync(PrintAttempt attempt, CancellationToken ct)
    {
        if (attempt.PrintJobId == Guid.Empty)
        {
            return;
        }

        await using GastronomyAppDbContext context = contextFactory();
        context.PrintAttempts.Add(attempt);
        await context.SaveChangesAsync(ct);
    }

    public async Task<PrintOutcomeApplied> ApplyOutcomeAsync(PrintOutcomeApplication application, CancellationToken ct)
    {
        await using GastronomyAppDbContext context = contextFactory();

        return await transactionRunner.RunAsync(
            context,
            async transactionCancellationToken =>
            {
                LocationTicket ticket = await context.LocationTickets
                    .SingleAsync(candidate => candidate.Id == application.LocationTicketId, transactionCancellationToken);

                PrintJob? job = await context.PrintJobs
                    .SingleOrDefaultAsync(candidate => candidate.Id == application.PrintJobId, transactionCancellationToken);

                if (job is not null)
                {
                    job.Status = application.JobStatus;
                    job.FailureReason = application.FailureReason;
                    job.CompletedAtUtc = IsTerminal(application.JobStatus) ? timeProvider.GetUtcNow().UtcDateTime : null;
                }

                bool takenByHuman = ticket.Status != LocationTicketStatus.Printing;
                if (!takenByHuman)
                {
                    ticket.Status = application.TicketStatus;
                }

                await context.SaveChangesAsync(transactionCancellationToken);

                return new TransactionOutcome<PrintOutcomeApplied>
                {
                    Value = new PrintOutcomeApplied
                    {
                        TicketStatus = ticket.Status,
                        TicketWasTakenByHuman = takenByHuman,
                    },
                    ShouldCommit = true,
                };
            },
            ct);
    }

    public async Task<IReadOnlyList<Guid>> LoadRecoverableTicketIdsAsync(IReadOnlyCollection<Guid> servedStationIds, CancellationToken ct)
    {
        await using GastronomyAppDbContext context = contextFactory();
        List<Guid> ids = servedStationIds.ToList();

        return await context.LocationTickets.AsNoTracking()
            .Where(ticket => ids.Contains(ticket.StationId)
                && (ticket.Status == LocationTicketStatus.Queued || ticket.Status == LocationTicketStatus.Blocked))
            .OrderBy(ticket => ticket.CreatedAtUtc)
            .Select(ticket => ticket.Id)
            .ToListAsync(ct);
    }

    public async Task MarkPrintingTicketsUnknownAsync(IReadOnlyCollection<Guid> servedStationIds, CancellationToken ct)
    {
        await using GastronomyAppDbContext context = contextFactory();
        List<Guid> ids = servedStationIds.ToList();

        List<LocationTicket> printing = await context.LocationTickets
            .Where(ticket => ids.Contains(ticket.StationId) && ticket.Status == LocationTicketStatus.Printing)
            .ToListAsync(ct);

        foreach (LocationTicket ticket in printing)
        {
            ticket.Status = LocationTicketStatus.Unknown;
        }

        await context.SaveChangesAsync(ct);
    }

    public async Task<int> CountWaitingTicketsAsync(Guid stationId, CancellationToken ct)
    {
        await using GastronomyAppDbContext context = contextFactory();

        return await context.LocationTickets.AsNoTracking()
            .CountAsync(
                ticket => ticket.StationId == stationId
                    && (ticket.Status == LocationTicketStatus.Queued || ticket.Status == LocationTicketStatus.Blocked),
                ct);
    }

    public async Task FailAllWaitingAtEndpointAsync(
        IReadOnlyCollection<Guid> servedStationIds,
        PrintFailureReason failureReason,
        CancellationToken ct)
    {
        await using GastronomyAppDbContext context = contextFactory();
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(ct);

        List<Guid> ids = servedStationIds.ToList();
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;

        List<PrinterStatus> statuses = await context.PrinterStatuses
            .Where(status => ids.Contains(status.StationId))
            .ToListAsync(ct);

        foreach (PrinterStatus status in statuses)
        {
            status.IsFaulty = true;
            status.LastChangedAtUtc = now;
            status.LastDetail = "The station circuit breaker tripped after two consecutive unknown outcomes.";
        }

        List<LocationTicket> waiting = await context.LocationTickets
            .Where(ticket => ids.Contains(ticket.StationId)
                && (ticket.Status == LocationTicketStatus.Queued || ticket.Status == LocationTicketStatus.Blocked))
            .ToListAsync(ct);

        List<Guid> waitingIds = [.. waiting.Select(ticket => ticket.Id)];
        List<PrintJob> jobs = await context.PrintJobs
            .Where(job => job.LocationTicketId != null && waitingIds.Contains(job.LocationTicketId.Value))
            .ToListAsync(ct);

        foreach (LocationTicket ticket in waiting)
        {
            ticket.Status = LocationTicketStatus.Failed;
        }

        foreach (PrintJob job in jobs.Where(candidate => !IsTerminal(candidate.Status)))
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
        await using GastronomyAppDbContext context = contextFactory();
        List<Guid> ids = servedStationIds.ToList();

        List<PrinterStatus> statuses = await context.PrinterStatuses
            .Where(status => ids.Contains(status.StationId))
            .ToListAsync(ct);

        foreach (PrinterStatus status in statuses)
        {
            status.IsFaulty = false;
            status.LastChangedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            status.LastDetail = "A human reconnected this printer.";
        }

        await context.SaveChangesAsync(ct);
    }

    public async Task<int> AllocateProcessIdAsync(string printerEndpointKey, CancellationToken ct)
    {
        await using GastronomyAppDbContext context = contextFactory();
        NumberCounterAllocator allocator = new(context);
        return await allocator.AllocatePrinterProcessIdAsync(printerEndpointKey, ct);
    }

    public async Task<IReadOnlyList<Guid>> OrderQueueAsync(IReadOnlyCollection<Guid> locationTicketIds, CancellationToken ct)
    {
        await using GastronomyAppDbContext context = contextFactory();
        List<Guid> ids = locationTicketIds.ToList();

        return await context.LocationTickets.AsNoTracking()
            .Where(ticket => ids.Contains(ticket.Id))
            .OrderBy(ticket => ticket.CreatedAtUtc)
            .Select(ticket => ticket.Id)
            .ToListAsync(ct);
    }

    public async Task<OrderTicketStatuses> LoadOrderTicketStatusesAsync(Guid orderId, CancellationToken ct)
    {
        await using GastronomyAppDbContext context = contextFactory();
        Order order = await context.Orders.AsNoTracking().SingleAsync(candidate => candidate.Id == orderId, ct);

        List<LocationTicketStatus> statuses = await context.LocationTickets.AsNoTracking()
            .Where(ticket => ticket.OrderId == orderId)
            .Select(ticket => ticket.Status)
            .ToListAsync(ct);

        return new OrderTicketStatuses
        {
            CurrentStatus = order.Status,
            TicketStatuses = statuses,
        };
    }

    public async Task WriteOrderStatusAsync(Guid orderId, OrderStatus status, CancellationToken ct)
    {
        await using GastronomyAppDbContext context = contextFactory();
        Order order = await context.Orders.SingleAsync(candidate => candidate.Id == orderId, ct);
        order.Status = status;
        await context.SaveChangesAsync(ct);
    }

    public async Task WritePrinterStatusAsync(Guid stationId, PrinterStatusSnapshot snapshot, CancellationToken ct)
    {
        await using GastronomyAppDbContext context = contextFactory();
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;

        PrinterStatus? status = await context.PrinterStatuses
            .SingleOrDefaultAsync(candidate => candidate.StationId == stationId, ct);

        if (status is null)
        {
            context.PrinterStatuses.Add(new PrinterStatus
            {
                StationId = stationId,
                IsOnline = snapshot.IsOnline,
                IsPaperEnd = snapshot.IsPaperEnd,
                IsPaperNearEnd = snapshot.IsPaperNearEnd,
                IsCoverOpen = snapshot.IsCoverOpen,
                IsInErrorState = snapshot.IsInErrorState,
                IsFaulty = false,
                LastDetail = snapshot.Detail,
                LastChangedAtUtc = now,
                LastHeardFromAtUtc = now,
            });
        }
        else
        {
            bool changed = status.IsOnline != snapshot.IsOnline
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

    public async Task<IReadOnlyList<SuspensionPeriod>> LoadSuspensionPeriodsAsync(
        Guid stationId,
        TransportKind transportKind,
        CancellationToken ct)
    {
        await using GastronomyAppDbContext context = contextFactory();

        PrinterStatus? status = await context.PrinterStatuses.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.StationId == stationId, ct);
        PrinterConfiguration? configuration = await context.PrinterConfigurations.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.StationId == stationId, ct);

        bool stationSwitchedOff = configuration is not null && !configuration.IsEnabled;
        bool folderUnwritable = transportKind == TransportKind.Mock && status is not null && status.IsInErrorState;
        bool mechanicalHold = status is not null && (status.IsPaperEnd || status.IsCoverOpen);

        if (!stationSwitchedOff && !folderUnwritable && !mechanicalHold)
        {
            return [];
        }

        DateTime startedAtUtc = status?.LastChangedAtUtc ?? DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
        return [new SuspensionPeriod { StartedAtUtc = startedAtUtc, EndedAtUtc = null }];
    }

    public async Task FailTicketAsync(Guid locationTicketId, Guid printJobId, PrintFailureReason failureReason, CancellationToken ct)
    {
        await using GastronomyAppDbContext context = contextFactory();
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;

        LocationTicket ticket = await context.LocationTickets.SingleAsync(candidate => candidate.Id == locationTicketId, ct);
        ticket.Status = LocationTicketStatus.Failed;

        PrintJob? job = await context.PrintJobs.SingleOrDefaultAsync(candidate => candidate.Id == printJobId, ct);
        if (job is not null)
        {
            job.Status = PrintJobStatus.Failed;
            job.FailureReason = failureReason;
            job.CompletedAtUtc = now;
        }

        await context.SaveChangesAsync(ct);
    }

    public async Task<LocationTicketStatus> FailJobOnlyAsync(
        Guid printJobId,
        PrintFailureReason failureReason,
        CancellationToken ct)
    {
        await using GastronomyAppDbContext context = contextFactory();
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;

        PrintJob? job = await context.PrintJobs.SingleOrDefaultAsync(candidate => candidate.Id == printJobId, ct);
        if (job is null)
        {
            return LocationTicketStatus.HandledOnPaper;
        }

        job.Status = PrintJobStatus.Failed;
        job.FailureReason = failureReason;
        job.CompletedAtUtc = now;
        await context.SaveChangesAsync(ct);

        if (job.LocationTicketId is null)
        {
            return LocationTicketStatus.HandledOnPaper;
        }

        return await context.LocationTickets.AsNoTracking()
            .Where(ticket => ticket.Id == job.LocationTicketId.Value)
            .Select(ticket => ticket.Status)
            .SingleAsync(ct);
    }

    public async Task<PrintJobEnsured> EnsureOpenPrintJobAsync(
        Guid locationTicketId,
        Guid stationId,
        PrintJobKind kind,
        CancellationToken ct)
    {
        await using GastronomyAppDbContext context = contextFactory();

        return await transactionRunner.RunAsync(
            context,
            async transactionCancellationToken =>
            {
                bool jobIsAlreadyOpen = await context.PrintJobs
                    .AnyAsync(
                        job => job.LocationTicketId == locationTicketId
                            && (job.Status == PrintJobStatus.Queued
                                || job.Status == PrintJobStatus.PreflightCheck
                                || job.Status == PrintJobStatus.Sending
                                || job.Status == PrintJobStatus.AwaitingEcho),
                        transactionCancellationToken);

                if (jobIsAlreadyOpen)
                {
                    return new TransactionOutcome<PrintJobEnsured>
                    {
                        Value = new PrintJobEnsured(false, null),
                        ShouldCommit = false,
                    };
                }

                DateTime now = timeProvider.GetUtcNow().UtcDateTime;
                Guid jobId = Guid.NewGuid();

                context.PrintJobs.Add(new PrintJob
                {
                    Id = jobId,
                    LocationTicketId = locationTicketId,
                    StationId = stationId,
                    Kind = kind,
                    Status = PrintJobStatus.Queued,
                    ProcessId = null,
                    FailureReason = null,
                    RequestedByDeviceId = null,
                    CreatedAtUtc = now,
                });

                if (kind == PrintJobKind.Reprint)
                {
                    LocationTicket ticket = await context.LocationTickets
                        .SingleAsync(candidate => candidate.Id == locationTicketId, transactionCancellationToken);
                    ticket.ReprintCount++;
                    ticket.Status = LocationTicketStatus.Queued;
                }

                await context.SaveChangesAsync(transactionCancellationToken);

                return new TransactionOutcome<PrintJobEnsured>
                {
                    Value = new PrintJobEnsured(true, jobId),
                    ShouldCommit = true,
                };
            },
            ct);
    }

    public async Task<Guid> CreatePrintJobAsync(
        Guid? locationTicketId,
        Guid stationId,
        PrintJobKind kind,
        CancellationToken ct)
    {
        await using GastronomyAppDbContext context = contextFactory();
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        Guid jobId = Guid.NewGuid();

        context.PrintJobs.Add(new PrintJob
        {
            Id = jobId,
            LocationTicketId = locationTicketId,
            StationId = stationId,
            Kind = kind,
            Status = PrintJobStatus.Queued,
            ProcessId = null,
            FailureReason = null,
            RequestedByDeviceId = null,
            CreatedAtUtc = now,
        });

        if (kind == PrintJobKind.Reprint && locationTicketId is not null)
        {
            LocationTicket ticket = await context.LocationTickets
                .SingleAsync(candidate => candidate.Id == locationTicketId.Value, ct);
            ticket.ReprintCount++;
            ticket.Status = LocationTicketStatus.Queued;
        }

        await context.SaveChangesAsync(ct);
        return jobId;
    }

    public async Task<TestPrintLoadResult> LoadTestPrintAsync(Guid stationId, CancellationToken ct)
    {
        await using GastronomyAppDbContext context = contextFactory();
        Station station = await context.Stations.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == stationId, ct);

        return new TestPrintLoadResult(station.Name);
    }

    public async Task<Guid?> ResolveStationAsync(Guid locationTicketId, CancellationToken ct)
    {
        await using GastronomyAppDbContext context = contextFactory();

        return await context.LocationTickets.AsNoTracking()
            .Where(ticket => ticket.Id == locationTicketId)
            .Select(ticket => (Guid?)ticket.StationId)
            .SingleOrDefaultAsync(ct);
    }

    private async Task<PrintJob?> LatestOpenJobAsync(GastronomyAppDbContext context, Guid locationTicketId, CancellationToken ct)
    {
        return await context.PrintJobs
            .Where(job => job.LocationTicketId == locationTicketId && job.CompletedAtUtc == null)
            .OrderByDescending(job => job.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);
    }

    private bool IsTerminal(PrintJobStatus status)
    {
        return status switch
        {
            PrintJobStatus.Confirmed => true,
            PrintJobStatus.Failed => true,
            PrintJobStatus.Unknown => true,
            PrintJobStatus.ResolvedPrinted => true,
            PrintJobStatus.ResolvedMissing => true,
            PrintJobStatus.Queued => false,
            PrintJobStatus.PreflightCheck => false,
            PrintJobStatus.Blocked => false,
            PrintJobStatus.Sending => false,
            PrintJobStatus.AwaitingEcho => false,
            _ => new Core.Services.Never().OfType<bool>(status),
        };
    }
}
