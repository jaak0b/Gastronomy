using GastronomyApp.Contracts.Admin.Catalog;
using GastronomyApp.Core.Entities;
using Mapster;

namespace GastronomyApp.Api.Mapping;

public sealed class AdminCategoryMapping : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<CatalogCategory, AdminCategoryView>().Map(view => view.CategoryId, category => category.Id);

    config.NewConfig<IReadOnlyList<CatalogCategory>, AdminCategoryListView>().Map(view => view.Categories, categories => categories);
  }
}
