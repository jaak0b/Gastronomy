namespace GastronomyApp.Core.ReadModels;

public sealed record OpenOrderItem
{
  public required Guid OrderItemId { get; init; }

  public required Guid OrderId { get; init; }

  public required int GlobalOrderNumber { get; init; }

  public required string ItemName { get; init; }

  public required string? Note { get; init; }

  public required int UnitPriceCents { get; init; }

  public required DateTime OrderedAtUtc { get; init; }
}
