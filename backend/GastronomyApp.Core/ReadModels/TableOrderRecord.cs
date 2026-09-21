namespace GastronomyApp.Core.ReadModels;

public sealed record TableOrderRecord
{
  public required Guid OrderId { get; init; }

  public required int GlobalOrderNumber { get; init; }

  public required DateTime CreatedAtUtc { get; init; }

  public required string StaffMemberName { get; init; }

  public required IReadOnlyList<TableOrderRecordItem> Items { get; init; }
}
