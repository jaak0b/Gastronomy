namespace GastronomyApp.Core.ReadModels;

public sealed record TableOrderReport
{
  public required string TableName { get; init; }

  public required int OpenAmountCents { get; init; }

  public required IReadOnlyList<TableOrderRecord> Orders { get; init; }
}
