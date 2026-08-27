namespace GastronomyApp.Api.Contracts;

public sealed record StationView(Guid StationId, string Name, int SortOrder, bool CanPrintRightNow);

public sealed record StationListView(IReadOnlyList<StationView> Stations);

public sealed record StationTicketLineView(int Quantity, string ItemName, string? LineNote);

public sealed record StationTicketView(
    Guid TicketId,
    Guid OrderId,
    Guid StationId,
    string StationName,
    int SequenceNumber,
    int GlobalOrderNumber,
    string TableLabel,
    string? OrderNote,
    DateTime OrderCreatedAtUtc,
    string Status,
    int ReprintCount,
    bool CanAcknowledge,
    string? CanAcknowledgeReasonKey,
    IReadOnlyList<StationTicketLineView> Lines);

public sealed record StationTicketListView(Guid StationId, IReadOnlyList<StationTicketView> Tickets);
