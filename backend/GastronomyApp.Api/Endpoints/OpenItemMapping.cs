using GastronomyApp.Api.Contracts;
using GastronomyApp.Core.ReadModels;
using Mapster;

namespace GastronomyApp.Api.Endpoints;

public sealed class OpenItemMapping : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<OpenOrderItem, OpenOrderItemView>();

    config.NewConfig<GivenAwayOrderItem, GivenAwayOrderItemView>();

    config.NewConfig<OpenTable, OpenTableView>();
  }
}
