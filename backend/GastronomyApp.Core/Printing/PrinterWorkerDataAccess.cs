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

public sealed record PrintJobLoadResult
{
  public required Guid PrintJobId { get; init; }
  public required Guid StationOrderId { get; init; }
  public required Guid OrderId { get; init; }
  public required Guid StationId { get; init; }
  public required int CopyNumber { get; init; }
  public required DateTime CreatedAtUtc { get; init; }
  public required PrintJobStatus Status { get; init; }
  public required int StationOrderNumber { get; init; }
  public required string StationName { get; init; }
  public required int GlobalOrderNumber { get; init; }
  public required string TableName { get; init; }
  public required string StaffMemberName { get; init; }
  public required string? OrderNote { get; init; }
  public required DateTime OrderCreatedAtUtc { get; init; }
  public required IReadOnlyList<PrintJobItemLoadResult> Items { get; init; }
  public required IReadOnlyList<string> AlsoGoesToStationNames { get; init; }
}

public sealed record PrintJobItemLoadResult(string ItemName, string? ItemNote);

public sealed record PrintOutcomeApplied
{
  public required PrintJobStatus PrintJobStatus { get; init; }
  public required bool WasHandledOnPaper { get; init; }
}

public sealed record OrderPrintJobStatuses
{
  public required OrderStatus CurrentStatus { get; init; }
  public required IReadOnlyList<PrintJobStatus> PrintJobStatuses { get; init; }
}

public sealed record PrintOutcomeApplication
{
  public required Guid PrintJobId { get; init; }
  public required PrintOutcome Outcome { get; init; }
  public required int BytesWritten { get; init; }
  public required PrinterStatusSnapshot StatusAtEnd { get; init; }
  public required PrintJobStatus JobStatus { get; init; }
  public PrintFailureReason? FailureReason { get; init; }
}

public sealed record PrintJobEnsured(bool WasCreated, Guid? PrintJobId, Guid? StationId);

public interface IPrinterWorkerDataAccess
{
  public Task<PrintJobEnsured> EnsureNextCopyAsync(Guid stationOrderId, CancellationToken ct);

  public Task<PrintJobLoadResult> LoadPrintJobAsync(Guid printJobId, CancellationToken ct);

  public Task<ClaimResult> TryClaimAsync(Guid printJobId, CancellationToken ct);

  public Task<PrintOutcomeApplied> ApplyOutcomeAsync(PrintOutcomeApplication application, CancellationToken ct);

  public Task<IReadOnlyList<Guid>> LoadRecoverablePrintJobIdsAsync(
      IReadOnlyCollection<Guid> servedStationIds,
      CancellationToken ct);

  public Task MarkSendingJobsUnknownAsync(IReadOnlyCollection<Guid> servedStationIds, CancellationToken ct);

  public Task<int> CountWaitingPrintJobsAsync(Guid stationId, CancellationToken ct);

  public Task FailAllWaitingAtEndpointAsync(
      IReadOnlyCollection<Guid> servedStationIds,
      PrintFailureReason failureReason,
      CancellationToken ct);

  public Task ClearFaultyAtEndpointAsync(IReadOnlyCollection<Guid> servedStationIds, CancellationToken ct);

  public Task<int> AllocatePrinterJobIdAsync(CancellationToken ct);

  public Task<IReadOnlyList<Guid>> OrderQueueAsync(IReadOnlyCollection<Guid> printJobIds, CancellationToken ct);

  public Task<OrderPrintJobStatuses> LoadOrderPrintJobStatusesAsync(Guid orderId, CancellationToken ct);

  public Task WritePrinterStatusAsync(Guid printerId, PrinterStatusSnapshot snapshot, CancellationToken ct);

  public Task<IReadOnlyList<SuspensionPeriod>> LoadSuspensionPeriodsAsync(Guid stationId, CancellationToken ct);

  public Task FailPrintJobAsync(Guid printJobId, PrintFailureReason failureReason, CancellationToken ct);

  public Task<Guid?> ResolveStationAsync(Guid printJobId, CancellationToken ct);
}
