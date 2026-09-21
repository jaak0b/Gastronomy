using GastronomyApp.Contracts.Enums;

namespace GastronomyApp.Contracts.Orders;

public sealed record OrderDeliveryModeRequest
{
  public required Guid StationId { get; init; }

  public required DeliveryMode DeliveryMode { get; init; }
}
