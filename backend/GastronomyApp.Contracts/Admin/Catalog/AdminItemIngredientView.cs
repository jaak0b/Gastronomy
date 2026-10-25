namespace GastronomyApp.Contracts.Admin.Catalog;

public sealed record AdminItemIngredientView(Guid IngredientId, string Name, Guid UnitId, string UnitName, double Amount);
