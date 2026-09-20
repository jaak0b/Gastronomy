using GastronomyApp.Api.Contracts;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Requests;
using Mapster;

namespace GastronomyApp.Api.Mapping;

public sealed class AdminCategoryMapping : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<SaveCategoryRequest, SaveCatalogCategoryRequest>();

    config.NewConfig<CatalogCategory, AdminCategoryView>().Map(view => view.CategoryId, category => category.Id);
  }
}
