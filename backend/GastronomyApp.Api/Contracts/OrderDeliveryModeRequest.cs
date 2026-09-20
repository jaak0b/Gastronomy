using GastronomyApp.Core.Enums;

namespace GastronomyApp.Api.Contracts;

public sealed record OrderDeliveryModeRequest
{
  public required Guid StationId { get; init; }

  public required DeliveryMode DeliveryMode { get; init; }
}
