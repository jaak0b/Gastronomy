using GastronomyApp.Core.Enums;

namespace GastronomyApp.Api.Contracts;

public sealed record OrderItemRequest
{
  public required Guid CatalogItemId { get; init; }

  public required int UnitPriceCents { get; init; }

  public string? Note { get; init; }

  public Guid? StationId { get; init; }
}

public sealed record OrderDeliveryModeRequest
{
  public required Guid StationId { get; init; }

  public required DeliveryMode DeliveryMode { get; init; }
}

public sealed record PlaceOrderRequest
{
  public required Guid ClientOrderId { get; init; }

  public required string? TableName { get; init; }

  public string? Note { get; init; }

  public bool SettleOnSend { get; init; }

  public required IReadOnlyList<OrderItemRequest>? Items { get; init; }

  public IReadOnlyList<OrderDeliveryModeRequest>? DeliveryModes { get; init; }
}

public sealed record StationOrderView(
  Guid StationOrderId,
  Guid StationId,
  string StationName,
  int StationOrderNumber,
  DeliveryMode DeliveryMode,
  IReadOnlyList<Guid> ItemIds);

public sealed record PlacedOrderView(
  Guid OrderId,
  int GlobalOrderNumber,
  OrderStatus Status,
  int TotalCents,
  DateTime CreatedAtUtc,
  IReadOnlyList<StationOrderView> StationOrders);

public sealed record OrderListStationOrderView(
  Guid StationOrderId,
  string StationName,
  int StationOrderNumber,
  DeliveryMode DeliveryMode,
  OrderStatus Status);

public sealed record OrderListEntryView(
  Guid OrderId,
  int GlobalOrderNumber,
  string TableName,
  int TotalCents,
  OrderStatus Status,
  DateTime CreatedAtUtc,
  IReadOnlyList<OrderListStationOrderView> StationOrders);

public sealed record OrderListView(IReadOnlyList<OrderListEntryView> Orders);
