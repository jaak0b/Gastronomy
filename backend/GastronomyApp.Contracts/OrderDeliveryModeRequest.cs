using GastronomyApp.Contracts.Enums;

namespace GastronomyApp.Contracts;

public sealed record OrderDeliveryModeRequest
{
  public required Guid StationId { get; init; }

  public required DeliveryMode DeliveryMode { get; init; }
}
