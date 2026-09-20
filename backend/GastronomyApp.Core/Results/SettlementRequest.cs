namespace GastronomyApp.Core.Results;

public sealed record SettlementRequest
{
  public required IReadOnlyList<SettlementLine> Lines { get; init; }

  public required Guid SettledByStaffMemberId { get; init; }
}
