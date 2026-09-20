namespace GastronomyApp.Core.Requests;

public sealed record SettlementRequest
{
  public required IReadOnlyList<SettlementLine> Lines { get; init; }

  public required Guid SettledByStaffMemberId { get; init; }
}
