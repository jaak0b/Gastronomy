using GastronomyApp.Core.Entities;
using GastronomyApp.Core.ReadModels;
using Mapster;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class CatalogProjection : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<CatalogCategory, CatalogCategoryRow>().Map(row => row.CategoryId, category => category.Id);
  }
}
