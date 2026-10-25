namespace GastronomyApp.Core.Results;

public sealed record ConsumedIngredient
{
  public required Guid IngredientId { get; init; }

  public required DateTime OrderedAtUtc { get; init; }

  public required double Amount { get; init; }
}
