namespace GastronomyApp.Contracts;

public sealed record StationQueueItemView(Guid OrderItemId, string ItemName, string? Note, DateTime? FulfilledAtUtc);
