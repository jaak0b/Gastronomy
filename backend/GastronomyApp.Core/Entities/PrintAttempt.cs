using GastronomyApp.Core.Enums;

namespace GastronomyApp.Core.Entities;

public sealed class PrintAttempt
{
    public required Guid Id { get; set; }
    public required Guid PrintJobId { get; set; }
    public required int AttemptNumber { get; set; }
    public required PrintAttemptOutcome Outcome { get; set; }
    public required PrintAttemptPhase Phase { get; set; }
    public required int BytesWritten { get; set; }
    public required string TransportDetail { get; set; }
    public required string PrinterStatusSnapshotJson { get; set; }
    public required DateTime StartedAtUtc { get; set; }
    public required DateTime EndedAtUtc { get; set; }
}
