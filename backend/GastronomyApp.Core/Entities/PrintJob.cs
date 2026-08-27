using GastronomyApp.Core.Enums;

namespace GastronomyApp.Core.Entities;

public sealed class PrintJob
{
    public required Guid Id { get; set; }
    public Guid? LocationTicketId { get; set; }
    public required Guid StationId { get; set; }
    public required PrintJobKind Kind { get; set; }
    public required PrintJobStatus Status { get; set; }
    public int? ProcessId { get; set; }
    public PrintFailureReason? FailureReason { get; set; }
    public Guid? RequestedByDeviceId { get; set; }
    public required DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}
