using GastronomyApp.Contracts.Enums;

namespace GastronomyApp.Contracts.Admin.Catalog;

public sealed record AdminItemIngredientView(Guid IngredientId, string Name, IngredientUnit Unit, double Amount);
