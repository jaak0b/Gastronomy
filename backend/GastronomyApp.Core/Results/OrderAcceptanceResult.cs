using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Results;

public sealed record OrderAcceptanceResult
{
  public required Order Order { get; init; }

  public required bool WasAlreadyAccepted { get; init; }
}

public sealed record OrderValidationFailure
{
  public required OrderValidationFailureReason Reason { get; init; }

  public Guid? OffendingCatalogItemId { get; init; }
}

public enum OrderValidationFailureReason
{
  NoItems,
  TooManyItems,
  TableNameMissing,
  UnknownCatalogItemId,
  StationRequired,
  StationNotAssignedToItem,
  ItemHasNoStation,
  PriceOutOfRange
}
