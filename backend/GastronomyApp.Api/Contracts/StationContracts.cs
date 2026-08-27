namespace GastronomyApp.Api.Contracts;

public sealed record StationLocationView(Guid LocationId, string Name, int SortOrder, bool CanPrintRightNow);

public sealed record StationLocationListView(IReadOnlyList<StationLocationView> Locations);

public sealed record StationTicketLineView(int Quantity, string ItemName, string? LineNote);

public sealed record StationTicketView(
    Guid TicketId,
    Guid OrderId,
    Guid LocationId,
    string LocationName,
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

public sealed record StationTicketListView(Guid LocationId, IReadOnlyList<StationTicketView> Tickets);
