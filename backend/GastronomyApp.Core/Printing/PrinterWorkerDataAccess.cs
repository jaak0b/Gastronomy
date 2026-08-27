using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Printing;

public enum ClaimOutcome
{
    Claimed,
    NoLongerWaiting,
    PrinterFaulty,
}

public sealed record ClaimResult
{
    public required ClaimOutcome Outcome { get; init; }
    public required Guid PrintJobId { get; init; }
}

public sealed record TicketLoadResult
{
    public required Guid LocationTicketId { get; init; }
    public required Guid OrderId { get; init; }
    public required Guid ProductionLocationId { get; init; }
    public required Guid PrintJobId { get; init; }
    public required PrintJobKind Kind { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public required LocationTicketStatus Status { get; init; }
    public required int ReprintCount { get; init; }
    public required int LocationSequenceNumber { get; init; }
    public required string ProductionLocationName { get; init; }
    public required int GlobalOrderNumber { get; init; }
    public required string TableLabel { get; init; }
    public required string StaffMemberName { get; init; }
    public required string? OrderNote { get; init; }
    public required DateTime OrderCreatedAtUtc { get; init; }
    public required IReadOnlyList<TicketLineLoadResult> Lines { get; init; }
    public required IReadOnlyList<string> AlsoGoesToStationNames { get; init; }
    public required string? ChosenStationNameIfDifferent { get; init; }
}

public sealed record TicketLineLoadResult(int Quantity, string ItemName, string? LineNote);

public sealed record TestPrintLoadResult(string ProductionLocationName);

public sealed record PrintOutcomeApplied
{
    public required LocationTicketStatus TicketStatus { get; init; }
    public required bool TicketWasTakenByHuman { get; init; }
}

public sealed record OrderTicketStatuses
{
    public required OrderStatus CurrentStatus { get; init; }
    public required IReadOnlyList<LocationTicketStatus> TicketStatuses { get; init; }
}

public sealed record PrintOutcomeApplication
{
    public required Guid LocationTicketId { get; init; }
    public required Guid PrintJobId { get; init; }
    public required PrintAttemptOutcome Outcome { get; init; }
    public required int BytesWritten { get; init; }
    public required PrinterStatusSnapshot StatusAtEnd { get; init; }
    public required PrintJobStatus JobStatus { get; init; }
    public required LocationTicketStatus TicketStatus { get; init; }
    public PrintFailureReason? FailureReason { get; init; }
}

public sealed record PrintJobEnsured(bool WasCreated, Guid? PrintJobId);

public interface IPrinterWorkerDataAccess
{
    public Task<PrintJobEnsured> EnsureOpenPrintJobAsync(
        Guid locationTicketId,
        Guid productionLocationId,
        PrintJobKind kind,
        CancellationToken ct);

    public Task<TicketLoadResult> LoadTicketForPrintingAsync(Guid locationTicketId, CancellationToken ct);

    public Task<ClaimResult> TryClaimAsync(Guid locationTicketId, CancellationToken ct);

    public Task RecordAttemptAsync(PrintAttempt attempt, CancellationToken ct);

    public Task<PrintOutcomeApplied> ApplyOutcomeAsync(PrintOutcomeApplication application, CancellationToken ct);

    public Task<IReadOnlyList<Guid>> LoadRecoverableTicketIdsAsync(IReadOnlyCollection<Guid> servedLocationIds, CancellationToken ct);

    public Task MarkPrintingTicketsUnknownAsync(IReadOnlyCollection<Guid> servedLocationIds, CancellationToken ct);

    public Task<int> CountWaitingTicketsAsync(Guid productionLocationId, CancellationToken ct);

    public Task FailAllWaitingAtEndpointAsync(IReadOnlyCollection<Guid> servedLocationIds, PrintFailureReason failureReason, CancellationToken ct);

    public Task ClearFaultyAtEndpointAsync(IReadOnlyCollection<Guid> servedLocationIds, CancellationToken ct);

    public Task<int> AllocateProcessIdAsync(string printerEndpointKey, CancellationToken ct);

    public Task<IReadOnlyList<Guid>> OrderQueueAsync(IReadOnlyCollection<Guid> locationTicketIds, CancellationToken ct);

    public Task<OrderTicketStatuses> LoadOrderTicketStatusesAsync(Guid orderId, CancellationToken ct);

    public Task WriteOrderStatusAsync(Guid orderId, OrderStatus status, CancellationToken ct);

    public Task WritePrinterStatusAsync(Guid productionLocationId, PrinterStatusSnapshot snapshot, CancellationToken ct);

    public Task<IReadOnlyList<SuspensionPeriod>> LoadSuspensionPeriodsAsync(Guid productionLocationId, TransportKind transportKind, CancellationToken ct);

    public Task FailTicketAsync(Guid locationTicketId, Guid printJobId, PrintFailureReason failureReason, CancellationToken ct);

    public Task<LocationTicketStatus> FailJobOnlyAsync(Guid printJobId, PrintFailureReason failureReason, CancellationToken ct);

    public Task<Guid> CreatePrintJobAsync(Guid? locationTicketId, Guid productionLocationId, PrintJobKind kind, CancellationToken ct);

    public Task<TestPrintLoadResult> LoadTestPrintAsync(Guid productionLocationId, CancellationToken ct);

    public Task<Guid?> ResolveProductionLocationAsync(Guid locationTicketId, CancellationToken ct);
}

