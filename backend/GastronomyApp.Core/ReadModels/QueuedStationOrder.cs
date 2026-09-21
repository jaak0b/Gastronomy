using GastronomyApp.Contracts.Enums;

namespace GastronomyApp.Core.ReadModels;

public sealed record QueuedStationOrder
{
  public required Guid StationOrderId { get; init; }

  public required int GlobalOrderNumber { get; init; }

  public required int StationOrderNumber { get; init; }

  public required string TableName { get; init; }

  public required string StaffMemberName { get; init; }

  public required DeliveryMode DeliveryMode { get; init; }

  public required DateTime CreatedAtUtc { get; init; }

  public required bool IsHiddenFromAsItComesQueue { get; init; }

  public required int ItemCount { get; init; }

  public required int FulfilledItemCount { get; init; }

  public required IReadOnlyList<QueuedOrderItem> Items { get; init; }
}
