namespace GastronomyApp.Core.ReadModels;

public sealed record OrderFulfillmentCounts
{
  public required Guid OrderId { get; init; }

  public required int ItemCount { get; init; }

  public required int FulfilledItemCount { get; init; }
}
