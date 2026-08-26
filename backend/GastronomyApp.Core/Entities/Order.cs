using GastronomyApp.Core.Enums;

namespace GastronomyApp.Core.Entities;

public sealed class Order
{
    public required Guid Id { get; set; }
    public required Guid EventSessionId { get; set; }
    public required Guid ClientOrderId { get; set; }
    public required int GlobalOrderNumber { get; set; }
    public required Guid ServerPersonId { get; set; }
    public required Guid DeviceId { get; set; }
    public required string TableLabel { get; set; }
    public string? Note { get; set; }
    public required int TotalCents { get; set; }
    public required OrderStatus Status { get; set; }
    public required DateTime CreatedAtUtc { get; set; }
    public List<OrderLine> Lines { get; set; } = [];
    public List<LocationTicket> Tickets { get; set; } = [];
}
