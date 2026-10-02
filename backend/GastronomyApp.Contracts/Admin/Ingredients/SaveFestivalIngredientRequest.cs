namespace GastronomyApp.Contracts.Admin.Ingredients;

public sealed record SaveFestivalIngredientRequest
{
  public required double? AvailableAmount { get; init; }
}
