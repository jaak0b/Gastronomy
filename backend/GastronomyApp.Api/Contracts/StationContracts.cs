using GastronomyApp.Core.Enums;

namespace GastronomyApp.Api.Contracts;

public sealed record StationView(Guid StationId, string Name, int SortOrder);

public sealed record StationListView(IReadOnlyList<StationView> Stations);

public sealed record StationQueueItemView(
  Guid OrderItemId,
  string ItemName,
  string? Note,
  DateTime? FulfilledAtUtc);

public sealed record StationQueueSliceView(
  Guid StationOrderId,
  int GlobalOrderNumber,
  int StationOrderNumber,
  string TableName,
  string? Note,
  DeliveryMode DeliveryMode,
  DateTime CreatedAtUtc,
  bool IsHiddenFromAsItComesQueue,
  int ItemCount,
  int FulfilledItemCount,
  IReadOnlyList<StationQueueItemView> Items);

public sealed record StationQueueView(
  StationSummaryView Station,
  IReadOnlyList<StationQueueSliceView> Orders,
  IReadOnlyList<StationQueueSliceView> AsItComes);

public sealed record StationFulfilledView(IReadOnlyList<StationQueueSliceView> Slices);

public sealed record StationItemSelectionRequest
{
  public required IReadOnlyList<Guid>? OrderItemIds { get; init; }
}

public sealed record StationEstimateView(Guid StationId, double QueuedMinutes);

public sealed record StationEstimateListView(IReadOnlyList<StationEstimateView> Stations);
