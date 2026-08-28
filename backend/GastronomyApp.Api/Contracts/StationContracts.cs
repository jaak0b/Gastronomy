namespace GastronomyApp.Api.Contracts;

public sealed record StationView(Guid StationId, string Name, int SortOrder, bool CanPrintRightNow);

public sealed record StationListView(IReadOnlyList<StationView> Stations);

public sealed record StationOrderItemView(int Quantity, string ItemName, string? ItemNote);

public sealed record StationScreenOrderView(
  Guid StationOrderId,
  Guid OrderId,
  Guid StationId,
  string StationName,
  int StationOrderNumber,
  int GlobalOrderNumber,
  string TableName,
  string? OrderNote,
  DateTime OrderCreatedAtUtc,
  string Status,
  int CopyNumber,
  bool CanHandleOnPaper,
  string? CanHandleOnPaperReasonKey,
  IReadOnlyList<StationOrderItemView> Items);

public sealed record StationScreenListView(Guid StationId, IReadOnlyList<StationScreenOrderView> StationOrders);
