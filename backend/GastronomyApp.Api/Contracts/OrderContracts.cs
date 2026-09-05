namespace GastronomyApp.Api.Contracts;

public sealed record OrderItemRequest
{
  public required Guid CatalogItemId { get; init; }

  public required int UnitPriceCents { get; init; }

  public string? Note { get; init; }

  public Guid? StationId { get; init; }
}

public sealed record PlaceOrderRequest
{
  public required Guid ClientOrderId { get; init; }

  public required string? TableName { get; init; }

  public string? Note { get; init; }

  public bool SettleOnSend { get; init; }

  public required IReadOnlyList<OrderItemRequest>? Items { get; init; }
}

public sealed record StationOrderView(
  Guid StationOrderId,
  Guid StationId,
  string StationName,
  int StationOrderNumber,
  string Status,
  IReadOnlyList<Guid> ItemIds);

public sealed record PlacedOrderView(
  Guid OrderId,
  int GlobalOrderNumber,
  string Status,
  int TotalCents,
  DateTime CreatedAtUtc,
  IReadOnlyList<StationOrderView> StationOrders);

public sealed record OrderListStationOrderView(
  Guid StationOrderId,
  string StationName,
  int StationOrderNumber,
  string Status,
  string? FailureReason,
  bool PrinterHasPaper);

public sealed record OrderListEntryView(
  Guid OrderId,
  int GlobalOrderNumber,
  string TableName,
  int TotalCents,
  string Status,
  DateTime CreatedAtUtc,
  IReadOnlyList<OrderListStationOrderView> StationOrders);

public sealed record OrderListView(IReadOnlyList<OrderListEntryView> Orders);

public sealed record ResolveUnknownPrintRequest
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
