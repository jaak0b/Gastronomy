using GastronomyApp.Contracts.Admin.Catalog;

namespace GastronomyApp.Core.Results;

public sealed record ItemMenuEntry(Guid CatalogItemId, AdminItemAtFestivalView AtTheFestival);
