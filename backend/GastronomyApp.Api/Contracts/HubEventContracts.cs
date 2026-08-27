namespace GastronomyApp.Api.Contracts;

public sealed record OrderAcceptedEvent(
    Guid OrderId,
    int GlobalOrderNumber,
    string TableLabel,
    int TotalCents,
    IReadOnlyList<OrderTicketView> Tickets);

public sealed record TicketStatusChangedEvent(
    Guid OrderId,
    int GlobalOrderNumber,
    Guid TicketId,
    Guid StationId,
    string StationName,
    int SequenceNumber,
    string Status,
    string? FailureReason,
    bool PrinterHasPaper,
    string? MessageKey,
    IReadOnlyDictionary<string, string> Parameters);

public sealed record OrderStatusChangedEvent(Guid OrderId, string Status);

public sealed record StationBacklogChangedEvent(Guid StationId);

public sealed record PrinterStatusChangedEvent(
    Guid StationId,
    string StationName,
    bool IsOnline,
    bool IsPaperEnd,
    bool IsPaperNearEnd,
    bool IsCoverOpen,
    bool IsFaulty,
    int WaitingTicketCount,
    DateTime LastChangedAtUtc,
    string LastDetail);

public sealed record CatalogChangedEvent(string Version);

public sealed record EnrolmentCompletedEvent(Guid StaffMemberId, string StaffMemberName, Guid DeviceId);

public sealed record DeviceRevokedEvent(Guid DeviceId);

