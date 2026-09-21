namespace GastronomyApp.Contracts;

public sealed record SaveItemRequest
{
  public required string? Name { get; init; }

  public required Guid? CategoryId { get; init; }

  public required int SortOrder { get; init; }

  public double? ProductionMinutes { get; init; }

  public bool IsQueueIndependent { get; init; }
}
