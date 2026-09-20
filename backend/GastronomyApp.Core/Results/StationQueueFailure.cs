namespace GastronomyApp.Core.Results;

public sealed record StationQueueFailure
{
  public required StationQueueFailureReason Reason { get; init; }

  public Guid? OffendingOrderItemId { get; init; }
}
