namespace GastronomyApp.Contracts.Admin.Ingredients;

public sealed record AdminIngredientView(Guid IngredientId, string Name, Guid UnitId, string UnitName, bool IsActive, IReadOnlyList<string> ActiveItemNames);
