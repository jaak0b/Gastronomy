using GastronomyApp.Core.Enums;

namespace GastronomyApp.Core.Printing;

public sealed record PrintPayload(
  int PrinterJobId,
  ReadOnlyMemory<byte> Bytes,
  string RenderedText,
  int CopyNumber,
  int StationOrderNumber,
  Guid StationId,
  string StationName,
  bool IsTest);

public sealed record PrinterStatusSnapshot(
  bool IsOnline,
  bool IsPaperEnd,
  bool IsPaperNearEnd,
  bool IsCoverOpen,
  bool IsInErrorState,
  string Detail,
  DateTimeOffset ObservedAt);

public sealed record PrintDispatchResult(
  PrintOutcome Outcome,
  int BytesWritten,
  PrinterStatusSnapshot StatusAtEnd,
  string Detail);

public interface IPrinterSession : IAsyncDisposable
{
  public IAsyncEnumerable<PrinterStatusSnapshot> StatusStream { get; }

  public Task<PrinterStatusSnapshot> QueryStatusAsync(CancellationToken cancellationToken);

  public Task<PrintDispatchResult> SendJobAsync(PrintPayload payload, CancellationToken cancellationToken);
}

public sealed record PrinterSessionTimeouts(
  TimeSpan JobTimeout,
  TimeSpan HeartbeatInterval,
  TimeSpan StatusQueryTimeout);
