using GastronomyApp.Api.Contracts;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Requests;
using Mapster;

namespace GastronomyApp.Api.Endpoints;

public sealed class AdminFestivalMapping : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<SaveFestivalRequest, FestivalPeriodRequest>();

    config.NewConfig<AdministeredFestival, AdminFestivalView>();
  }
}
