namespace GastronomyApp.Api.Contracts;

public sealed record OrderLineRequest
{
    public required Guid CatalogItemId { get; init; }
    public required int Quantity { get; init; }
    public string? Note { get; init; }
    public Guid? StationId { get; init; }
}

public sealed record PlaceOrderRequest
{
    public required Guid ClientOrderId { get; init; }
    public required string? TableLabel { get; init; }
    public string? Note { get; init; }
    public int? ExpectedTotalCents { get; init; }
    public required IReadOnlyList<OrderLineRequest>? Lines { get; init; }
}

public sealed record OrderTicketView(
    Guid TicketId,
    Guid StationId,
    string StationName,
    int SequenceNumber,
    string Status,
    IReadOnlyList<Guid> LineIds);

public sealed record PlacedOrderView(
    Guid OrderId,
    int GlobalOrderNumber,
    string Status,
    int TotalCents,
    int ExpectedTotalCents,
    DateTime CreatedAtUtc,
    IReadOnlyList<OrderTicketView> Tickets);

public sealed record OrderListTicketView(
    Guid TicketId,
    string StationName,
    int SequenceNumber,
    string Status,
    string? FailureReason,
    bool PrinterHasPaper);

public sealed record OrderListEntryView(
    Guid OrderId,
    int GlobalOrderNumber,
    string TableLabel,
    int TotalCents,
    string Status,
    DateTime CreatedAtUtc,
    IReadOnlyList<OrderListTicketView> Tickets);

public sealed record OrderListView(IReadOnlyList<OrderListEntryView> Orders);

public sealed record OrderDetailLineView(
    Guid LineId,
    Guid CatalogItemId,
    string ItemName,
    int Quantity,
    int UnitPriceCents,
    string? Note,
    string StationName);

public sealed record OrderDetailView(
    Guid OrderId,
    int GlobalOrderNumber,
    string TableLabel,
    string? Note,
    int TotalCents,
    string Status,
    DateTime CreatedAtUtc,
    IReadOnlyList<OrderDetailLineView> Lines,
    IReadOnlyList<OrderTicketView> Tickets);

public sealed record ResolveTicketRequest
{
    public required bool SlipIsOnThePile { get; init; }
}

public sealed record PrinterStatusView(
    Guid StationId,
    string Name,
    bool IsOnline,
    bool IsPaperEnd,
    bool IsPaperNearEnd,
    bool IsCoverOpen,
    bool IsFaulty,
    DateTime LastChangedAtUtc);

public sealed record PrinterStatusListView(IReadOnlyList<PrinterStatusView> Stations);
