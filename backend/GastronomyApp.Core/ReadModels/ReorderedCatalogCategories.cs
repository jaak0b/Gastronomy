using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.ReadModels;

public sealed record ReorderedCatalogCategories(IReadOnlyList<CatalogCategory> Categories, bool OrderChanged);
