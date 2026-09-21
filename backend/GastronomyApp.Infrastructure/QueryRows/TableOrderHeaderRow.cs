namespace GastronomyApp.Infrastructure.QueryRows;

public sealed record TableOrderHeaderRow
{
  public required Guid OrderId { get; init; }

  public required int GlobalOrderNumber { get; init; }

  public required DateTime CreatedAtUtc { get; init; }

  public required string StaffMemberName { get; init; }
}
