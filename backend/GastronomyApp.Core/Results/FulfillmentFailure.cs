namespace GastronomyApp.Core.Results;

public sealed record FulfillmentFailure
{
  public required FulfillmentFailureReason Reason { get; init; }

  public Guid? OffendingOrderItemId { get; init; }
}
