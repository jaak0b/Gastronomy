namespace GastronomyApp.Core.ReadModels;

public sealed record OpenItemsReport
{
  public required IReadOnlyList<OpenTable> Tables { get; init; }

  public required IReadOnlyList<Guid> OrderItemIdsWithoutAnOrder { get; init; }
}
