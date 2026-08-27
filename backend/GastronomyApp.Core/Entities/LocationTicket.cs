using GastronomyApp.Core.Enums;

namespace GastronomyApp.Core.Entities;

public sealed class LocationTicket
{
    public required Guid Id { get; set; }
    public required Guid OrderId { get; set; }
    public required Guid StationId { get; set; }
    public required int StationSequenceNumber { get; set; }
    public required LocationTicketStatus Status { get; set; }
    public required int ReprintCount { get; set; }
    public required DateTime CreatedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public string? ResolutionNote { get; set; }
}
