namespace GastronomyApp.Api.Contracts;

public sealed record OrderAcceptedEvent(
  Guid OrderId,
  int GlobalOrderNumber,
  string TableName,
  int TotalCents,
  IReadOnlyList<StationOrderView> StationOrders);

public sealed record PrintJobStatusChangedEvent(
  Guid OrderId,
  int GlobalOrderNumber,
  Guid StationOrderId,
  Guid StationId,
  string StationName,
  int StationOrderNumber,
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
  int WaitingPrintJobCount,
  DateTime LastChangedAtUtc,
  string LastDetail);

public sealed record CatalogChangedEvent(string Version);

public sealed record EnrolmentCompletedEvent(Guid StaffMemberId, string StaffMemberName, Guid DeviceId);

public sealed record DeviceRevokedEvent(Guid DeviceId);
