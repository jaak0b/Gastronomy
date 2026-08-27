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
    Guid LocationId,
    string LocationName,
    int SequenceNumber,
    string Status,
    string? FailureReason,
    bool PrinterHasPaper,
    string? MessageKey,
    IReadOnlyDictionary<string, string> Parameters);

public sealed record OrderStatusChangedEvent(Guid OrderId, string Status);

public sealed record PrinterStatusChangedEvent(
    Guid LocationId,
    string LocationName,
    bool IsOnline,
    bool IsPaperEnd,
    bool IsPaperNearEnd,
    bool IsCoverOpen,
    bool IsFaulty,
    int WaitingTicketCount,
    string LastDetail);

public sealed record CatalogChangedEvent(string Version);

public sealed record EnrolmentCompletedEvent(Guid ServerPersonId, string ServerPersonName, Guid DeviceId);

public sealed record DeviceRevokedEvent(Guid DeviceId);

public sealed record EventSessionStartedEvent(Guid EventSessionId, string Name, bool IsPractice);
