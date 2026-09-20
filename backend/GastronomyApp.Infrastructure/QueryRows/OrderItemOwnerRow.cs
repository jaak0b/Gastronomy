using GastronomyApp.Core.Entities;

namespace GastronomyApp.Infrastructure.QueryRows;

public sealed record OrderItemOwnerRow
{
  public required Guid OrderItemId { get; init; }

  public required Order Order { get; init; }
}
