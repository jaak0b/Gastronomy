namespace GastronomyApp.Contracts.Admin.Units;

public sealed record AdminUnitView(Guid UnitId, string Name, IReadOnlyList<string> UsedByIngredientNames);
