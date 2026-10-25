using GastronomyApp.Contracts.Admin.Catalog;
using GastronomyApp.Core.Entities;
using Mapster;

namespace GastronomyApp.Core.Mapping;

public sealed class CatalogCategoryMapping : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<CatalogCategory, AdminCategoryView>().Map(view => view.CategoryId, category => category.Id);
  }
}
