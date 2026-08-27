using GastronomyApp.Core.Enums;

namespace GastronomyApp.Core.Printing;

public sealed record PrinterEndpoint(
    Guid ProductionLocationId,
    TransportKind TransportKind,
    string? Host,
    int Port,
    string? AgentIdentifier,
    TimeSpan ConnectTimeout,
    TimeSpan JobTimeout,
    TimeSpan HeartbeatInterval,
    TimeSpan StatusQueryTimeout);

public sealed record PrintPayload(
    int ProcessId,
    ReadOnlyMemory<byte> Bytes,
    string RenderedText,
    PrintJobKind Kind,
    int LocationSequenceNumber,
    int ReprintCount,
    Guid ProductionLocationId,
    string ProductionLocationName);

public sealed record PrinterStatusSnapshot(
    bool IsOnline,
    bool IsPaperEnd,
    bool IsPaperNearEnd,
    bool IsCoverOpen,
    bool IsInErrorState,
    string Detail,
    DateTimeOffset ObservedAt);

public sealed record PrintDispatchResult(
    PrintAttemptOutcome Outcome,
    int BytesWritten,
    PrinterStatusSnapshot StatusAtEnd,
    string Detail);

public interface IPrinterTransport
{
    public TransportKind Kind { get; }

    public Task<IPrinterSession> ConnectAsync(PrinterEndpoint endpoint, CancellationToken cancellationToken);
}

public interface IPrinterSession : IAsyncDisposable
{
    public IAsyncEnumerable<PrinterStatusSnapshot> StatusStream { get; }

    public Task<PrinterStatusSnapshot> QueryStatusAsync(CancellationToken cancellationToken);

    public Task<PrintDispatchResult> SendJobAsync(PrintPayload payload, CancellationToken cancellationToken);
}
