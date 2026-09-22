using GastronomyApp.Core.Services;
using Mapster;

namespace GastronomyApp.Api.Mapping;

public sealed class MappingConfiguration
{
  private readonly OrderService _orderService;

  public MappingConfiguration(OrderService orderService)
  {
    _orderService = orderService;
  }

  public TypeAdapterConfig Build()
  {
    TypeAdapterConfig config = new() { RequireDestinationMemberSource = true };

    config.Scan(typeof(MappingConfiguration).Assembly);
    new OrderMapping(_orderService).Register(config);
    config.Compile();

    return config;
  }
}
