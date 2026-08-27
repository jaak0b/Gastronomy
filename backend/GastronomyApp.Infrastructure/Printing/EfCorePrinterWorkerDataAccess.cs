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
    private readonly Uri stationBaseUri;
    private readonly TimeProvider timeProvider;

    public EfCorePrinterWorkerDataAccess(
        Func<GastronomyAppDbContext> contextFactory,
        Uri stationBaseUri,
        TimeProvider timeProvider)
    {
        this.contextFactory = contextFactory;
        this.stationBaseUri = stationBaseUri;
        this.timeProvider = timeProvider;
    }

    public async Task<TicketLoadResult> LoadTicketForPrintingAsync(Guid locationTicketId, CancellationToken ct)
    {
        await using GastronomyAppDbContext context = contextFactory();

        LocationTicket ticket = await context.LocationTickets.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == locationTicketId, ct);
        Order order = await context.Orders.AsNoTracking().SingleAsync(candidate => candidate.Id == ticket.OrderId, ct);
        ProductionLocation location = await context.ProductionLocations.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == ticket.ProductionLocationId, ct);

        List<OrderLine> lines = await context.OrderLines.AsNoTracking()
            .Where(line => line.LocationTicketId == locationTicketId)
            .ToListAsync(ct);

        List<Guid> siblingLocationIds = await context.LocationTickets.AsNoTracking()
            .Where(candidate => candidate.OrderId == ticket.OrderId && candidate.Id != locationTicketId)
            .Select(candidate => candidate.ProductionLocationId)
            .Distinct()
            .ToListAsync(ct);

        List<string> siblingNames = await context.ProductionLocations.AsNoTracking()
            .Where(candidate => siblingLocationIds.Contains(candidate.Id))
            .OrderBy(candidate => candidate.SortOrder)
            .Select(candidate => candidate.Name)
            .ToListAsync(ct);

        ServerPerson? serverPerson = await context.ServerPeople.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == order.ServerPersonId, ct);

        Guid? chosenElsewhere = lines
            .Select(line => line.ChosenProductionLocationId)
            .FirstOrDefault(chosen => chosen is not null && chosen != ticket.ProductionLocationId);

        string? chosenName = chosenElsewhere is null
            ? null
            : await context.ProductionLocations.AsNoTracking()
                .Where(candidate => candidate.Id == chosenElsewhere.Value)
                .Select(candidate => candidate.Name)
                .SingleOrDefaultAsync(ct);

        PrintJob? job = await LatestOpenJobAsync(context, locationTicketId, ct);

        return new TicketLoadResult
        {
            LocationTicketId = ticket.Id,
            OrderId = ticket.OrderId,
            ProductionLocationId = ticket.ProductionLocationId,
            PrintJobId = job?.Id ?? Guid.Empty,
            Kind = job?.Kind ?? PrintJobKind.Initial,
            CreatedAtUtc = ticket.CreatedAtUtc,
            Status = ticket.Status,
            ReprintCount = ticket.ReprintCount,
            LocationSequenceNumber = ticket.LocationSequenceNumber,
            ProductionLocationName = location.Name,
            SlipLanguage = location.SlipLanguage,
            GlobalOrderNumber = order.GlobalOrderNumber,
            TableLabel = order.TableLabel,
            ServerName = serverPerson?.Name ?? string.Empty,
            OrderNote = order.Note,
            OrderCreatedAtUtc = order.CreatedAtUtc,
            Lines = [.. lines.Select(line => new TicketLineLoadResult(line.Quantity, line.ItemNameSnapshot, line.Note))],
            AlsoGoesToStationNames = siblingNames,
            ChosenStationNameIfDifferent = chosenName,
            StationCardUrl = new Uri(stationBaseUri, $"station/{location.StationAccessKey}"),
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
                        candidate => candidate.ProductionLocationId == ticket.ProductionLocationId,
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

    public async Task<IReadOnlyList<Guid>> LoadRecoverableTicketIdsAsync(IReadOnlyCollection<Guid> servedLocationIds, CancellationToken ct)
    {
        await using GastronomyAppDbContext context = contextFactory();
        List<Guid> ids = servedLocationIds.ToList();

        return await context.LocationTickets.AsNoTracking()
            .Where(ticket => ids.Contains(ticket.ProductionLocationId)
                && (ticket.Status == LocationTicketStatus.Queued || ticket.Status == LocationTicketStatus.Blocked))
            .OrderBy(ticket => ticket.CreatedAtUtc)
            .Select(ticket => ticket.Id)
            .ToListAsync(ct);
    }

    public async Task MarkPrintingTicketsUnknownAsync(IReadOnlyCollection<Guid> servedLocationIds, CancellationToken ct)
    {
        await using GastronomyAppDbContext context = contextFactory();
        List<Guid> ids = servedLocationIds.ToList();

        List<LocationTicket> printing = await context.LocationTickets
            .Where(ticket => ids.Contains(ticket.ProductionLocationId) && ticket.Status == LocationTicketStatus.Printing)
            .ToListAsync(ct);

        foreach (LocationTicket ticket in printing)
        {
            ticket.Status = LocationTicketStatus.Unknown;
        }

        await context.SaveChangesAsync(ct);
    }

    public async Task<int> CountWaitingTicketsAsync(Guid productionLocationId, CancellationToken ct)
    {
        await using GastronomyAppDbContext context = contextFactory();

        return await context.LocationTickets.AsNoTracking()
            .CountAsync(
                ticket => ticket.ProductionLocationId == productionLocationId
                    && (ticket.Status == LocationTicketStatus.Queued || ticket.Status == LocationTicketStatus.Blocked),
                ct);
    }

    public async Task FailAllWaitingAtEndpointAsync(
        IReadOnlyCollection<Guid> servedLocationIds,
        PrintFailureReason failureReason,
        CancellationToken ct)
    {
        await using GastronomyAppDbContext context = contextFactory();
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(ct);

        List<Guid> ids = servedLocationIds.ToList();
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;

        List<PrinterStatus> statuses = await context.PrinterStatuses
            .Where(status => ids.Contains(status.ProductionLocationId))
            .ToListAsync(ct);

        foreach (PrinterStatus status in statuses)
        {
            status.IsFaulty = true;
            status.LastChangedAtUtc = now;
            status.LastDetail = "The station circuit breaker tripped after two consecutive unknown outcomes.";
        }

        List<LocationTicket> waiting = await context.LocationTickets
            .Where(ticket => ids.Contains(ticket.ProductionLocationId)
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

    public async Task ClearFaultyAtEndpointAsync(IReadOnlyCollection<Guid> servedLocationIds, CancellationToken ct)
    {
        await using GastronomyAppDbContext context = contextFactory();
        List<Guid> ids = servedLocationIds.ToList();

        List<PrinterStatus> statuses = await context.PrinterStatuses
            .Where(status => ids.Contains(status.ProductionLocationId))
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

    public async Task WritePrinterStatusAsync(Guid productionLocationId, PrinterStatusSnapshot snapshot, CancellationToken ct)
    {
        await using GastronomyAppDbContext context = contextFactory();
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;

        PrinterStatus? status = await context.PrinterStatuses
            .SingleOrDefaultAsync(candidate => candidate.ProductionLocationId == productionLocationId, ct);

        if (status is null)
        {
            context.PrinterStatuses.Add(new PrinterStatus
            {
                ProductionLocationId = productionLocationId,
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
        Guid productionLocationId,
        TransportKind transportKind,
        CancellationToken ct)
    {
        await using GastronomyAppDbContext context = contextFactory();

        PrinterStatus? status = await context.PrinterStatuses.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.ProductionLocationId == productionLocationId, ct);
        PrinterConfiguration? configuration = await context.PrinterConfigurations.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.ProductionLocationId == productionLocationId, ct);

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

    public async Task<Guid> CreatePrintJobAsync(
        Guid? locationTicketId,
        Guid productionLocationId,
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
            ProductionLocationId = productionLocationId,
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

    public async Task<TestPrintLoadResult> LoadTestPrintAsync(Guid productionLocationId, CancellationToken ct)
    {
        await using GastronomyAppDbContext context = contextFactory();
        ProductionLocation location = await context.ProductionLocations.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == productionLocationId, ct);

        return new TestPrintLoadResult(
            location.Name,
            location.SlipLanguage,
            new Uri(stationBaseUri, $"station/{location.StationAccessKey}"));
    }

    public async Task<Guid?> ResolveProductionLocationAsync(Guid locationTicketId, CancellationToken ct)
    {
        await using GastronomyAppDbContext context = contextFactory();

        return await context.LocationTickets.AsNoTracking()
            .Where(ticket => ticket.Id == locationTicketId)
            .Select(ticket => (Guid?)ticket.ProductionLocationId)
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
