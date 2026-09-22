using GastronomyApp.Core.Services;
using Mapster;

namespace GastronomyApp.Api.Mapping;

public sealed class MappingConfiguration
{
  private readonly OrderService _orderService;
  private readonly StationOrderService _stationOrderService;

  public MappingConfiguration(OrderService orderService, StationOrderService stationOrderService)
  {
    _orderService = orderService;
    _stationOrderService = stationOrderService;
  }

  public TypeAdapterConfig Build()
  {
    TypeAdapterConfig config = new() { RequireDestinationMemberSource = true };

    config.Scan(typeof(MappingConfiguration).Assembly);
    new OrderMapping(_orderService).Register(config);
    new StationQueueMapping(_stationOrderService).Register(config);
    config.Compile();

    return config;
  }
}
