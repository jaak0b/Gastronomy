using GastronomyApp.Contracts;
using GastronomyApp.Core.Entities;
using Mapster;

namespace GastronomyApp.Api.Mapping;

public sealed class AdminCategoryMapping : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<CatalogCategory, AdminCategoryView>().Map(view => view.CategoryId, category => category.Id);
  }
}
