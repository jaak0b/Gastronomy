using GastronomyApp.Core.Entities;

namespace GastronomyApp.Infrastructure.QueryRows;

public sealed record TableOrderItemRow
{
  public required Guid OrderId { get; init; }

  public required OrderItem Item { get; init; }
}
