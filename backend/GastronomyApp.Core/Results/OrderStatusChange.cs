using GastronomyApp.Core.Enums;

namespace GastronomyApp.Core.Results;

public sealed record OrderStatusChange
{
  public required Guid OrderId { get; init; }

  public required OrderStatus Status { get; init; }
}
