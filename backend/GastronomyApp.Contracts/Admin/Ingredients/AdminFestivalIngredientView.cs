using GastronomyApp.Contracts.Enums;

namespace GastronomyApp.Contracts.Admin.Ingredients;

public sealed record AdminFestivalIngredientView(Guid IngredientId, string Name, IngredientUnit Unit, bool IsActive, double? AvailableAmount, double UsedAmount, DateTime? RunsOutAtUtc);
