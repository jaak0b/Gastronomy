using GastronomyApp.Core.Enums;

namespace GastronomyApp.Core.Requests;

public sealed record StationDeliveryModeRequest
{
  public required Guid StationId { get; init; }

  public required DeliveryMode DeliveryMode { get; init; }
}
