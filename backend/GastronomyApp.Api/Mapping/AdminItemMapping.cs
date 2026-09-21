using GastronomyApp.Contracts;
using GastronomyApp.Core.ReadModels;
using Mapster;

namespace GastronomyApp.Api.Mapping;

public sealed class AdminItemMapping : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<CatalogItemAtFestival, AdminItemAtFestivalView>();

    config.NewConfig<AdministeredCatalogItem, AdminItemView>();
  }
}
