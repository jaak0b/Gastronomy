namespace GastronomyApp.Contracts.Admin.Ingredients;

public sealed record AdminFestivalIngredientListView(IReadOnlyList<AdminFestivalIngredientView> Ingredients);
