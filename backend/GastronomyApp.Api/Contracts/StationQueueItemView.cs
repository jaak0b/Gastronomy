namespace GastronomyApp.Api.Contracts;

public sealed record StationQueueItemView(
  Guid OrderItemId,
  string ItemName,
  string? Note,
  DateTime? FulfilledAtUtc);
