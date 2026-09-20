using GastronomyApp.Api.Contracts;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Requests;
using Mapster;

namespace GastronomyApp.Api.Mapping;

public sealed class AdminStationMapping : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<SaveStationRequest, SaveStationDetailsRequest>();

    config.NewConfig<AdministeredStation, AdminStationView>();
  }
}
