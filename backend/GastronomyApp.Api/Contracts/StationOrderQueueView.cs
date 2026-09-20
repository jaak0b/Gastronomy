using GastronomyApp.Core.Enums;

namespace GastronomyApp.Api.Contracts;

public sealed record StationOrderQueueView(
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
