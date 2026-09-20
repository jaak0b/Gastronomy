namespace GastronomyApp.Core.ReadModels;

public sealed record OpenTable
{
  public required string TableName { get; init; }

  public required int OpenAmountCents { get; init; }

  public required IReadOnlyList<OpenOrderItem> Items { get; init; }
}
