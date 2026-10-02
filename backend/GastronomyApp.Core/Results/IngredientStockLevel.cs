using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Results;

public sealed record IngredientStockLevel(FestivalIngredient Stock, double UsedAmount, DateTime? RunsOutAtUtc);
