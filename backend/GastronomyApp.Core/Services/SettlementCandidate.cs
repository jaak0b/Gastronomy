using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Services;

public sealed record SettlementCandidate
{
  public required OrderItem Item { get; init; }

  public required string TableName { get; init; }
}
