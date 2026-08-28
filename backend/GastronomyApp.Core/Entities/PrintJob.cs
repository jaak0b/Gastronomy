using GastronomyApp.Core.Enums;

namespace GastronomyApp.Core.Entities;

public sealed class PrintJob
{
  public required Guid Id { get; set; }

  public required Guid StationOrderId { get; set; }

  public required int CopyNumber { get; set; }

  public required PrintJobStatus Status { get; set; }

  public PrintFailureReason? FailureReason { get; set; }

  // The printer echoes this number back once the paper is out, which is how a print is confirmed
  // rather than assumed. ESC/POS carries it as four ASCII digits, so it wraps at 9999.
  public int? PrinterJobId { get; set; }

  public required DateTime CreatedAtUtc { get; set; }

  public DateTime? CompletedAtUtc { get; set; }
}
