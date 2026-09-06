using GastronomyApp.Core.Enums;

namespace GastronomyApp.Api.Contracts;

public sealed record StationView(Guid StationId, string Name, int SortOrder);

public sealed record StationListView(IReadOnlyList<StationView> Stations);

public sealed record StationQueueItemView(
  Guid OrderItemId,
  string ItemName,
  string? Note,
  ProductionStatus ProductionStatus);

public sealed record StationQueueSliceView(
  Guid StationOrderId,
  int GlobalOrderNumber,
  int StationOrderNumber,
  string TableName,
  string? Note,
  DeliveryMode DeliveryMode,
  DateTime CreatedAtUtc,
  IReadOnlyList<StationQueueItemView> Items);

public sealed record StationQueueView(
  StationSummaryView Station,
  IReadOnlyList<StationQueueSliceView> Slices);

public sealed record StationItemStatusRequest
{
  public required IReadOnlyList<Guid>? OrderItemIds { get; init; }

  public required ProductionStatus Status { get; init; }
}

public sealed record StationItemStatusView(
  string? TableName,
  IReadOnlyList<StationQueueSliceView> Slices);

public sealed record StationEstimateView(Guid StationId, int QueuedMinutes);

public sealed record StationEstimateListView(IReadOnlyList<StationEstimateView> Stations);
