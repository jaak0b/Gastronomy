namespace GastronomyApp.Contracts.Admin.Ingredients;

public sealed record AdminIngredientListView(IReadOnlyList<AdminIngredientView> Ingredients);
