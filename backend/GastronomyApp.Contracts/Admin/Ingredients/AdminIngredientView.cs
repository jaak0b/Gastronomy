using GastronomyApp.Contracts.Enums;

namespace GastronomyApp.Contracts.Admin.Ingredients;

public sealed record AdminIngredientView(Guid IngredientId, string Name, IngredientUnit Unit, bool IsActive);
