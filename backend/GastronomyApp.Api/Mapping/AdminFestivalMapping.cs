using GastronomyApp.Contracts;
using GastronomyApp.Core.ReadModels;
using Mapster;

namespace GastronomyApp.Api.Mapping;

public sealed class AdminFestivalMapping : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<AdministeredFestival, AdminFestivalView>();
  }
}
