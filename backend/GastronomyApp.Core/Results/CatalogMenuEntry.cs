using GastronomyApp.Contracts.Catalog;

namespace GastronomyApp.Core.Results;

public sealed record CatalogMenuEntry(CatalogCategoryView Category, CatalogItemView Item);
