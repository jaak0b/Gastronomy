namespace GastronomyApp.Contracts.Stations;

public sealed record StationQueueItemView(Guid OrderItemId, string ItemName, string? Note, DateTime? FulfilledAtUtc);
