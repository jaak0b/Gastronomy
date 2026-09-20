using GastronomyApp.Api.Contracts;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Requests;
using Mapster;

namespace GastronomyApp.Api.Mapping;

public sealed class AdminItemMapping : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<SaveItemRequest, SaveCatalogItemRequest>();

    config.NewConfig<CatalogItemAtFestival, AdminItemAtFestivalView>();

    config.NewConfig<AdministeredCatalogItem, AdminItemView>();
  }
}
