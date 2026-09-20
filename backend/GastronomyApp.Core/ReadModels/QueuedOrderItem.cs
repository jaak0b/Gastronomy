namespace GastronomyApp.Core.ReadModels;

public sealed record QueuedOrderItem
{
  public required Guid OrderItemId { get; init; }

  public required string ItemName { get; init; }

  public required string? Note { get; init; }

  public required DateTime? FulfilledAtUtc { get; init; }
}
