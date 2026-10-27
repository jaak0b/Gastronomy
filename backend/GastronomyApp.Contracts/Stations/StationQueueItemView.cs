using GastronomyApp.Contracts.Enums;

namespace GastronomyApp.Contracts.Stations;

public sealed record StationQueueItemView(Guid OrderItemId, string ItemName, string? Note, OrderItemState State, DateTime StateSetAtUtc);
