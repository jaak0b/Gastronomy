using GastronomyApp.Core.Services;
using Mapster;

namespace GastronomyApp.Api.Mapping;

public sealed class MappingConfiguration
{
  private readonly OrderService _orderService;
  private readonly StationOrderService _stationOrderService;
  private readonly StationService _stationService;

  public MappingConfiguration(OrderService orderService, StationOrderService stationOrderService, StationService stationService)
  {
    _orderService = orderService;
    _stationOrderService = stationOrderService;
    _stationService = stationService;
  }

  public TypeAdapterConfig Build()
  {
    TypeAdapterConfig config = new() { RequireDestinationMemberSource = true };

    config.Scan(typeof(MappingConfiguration).Assembly);
    new OrderMapping(_orderService).Register(config);
    new StationQueueMapping(_stationOrderService).Register(config);
    new AdminStationMapping(_stationService).Register(config);
    new StationEstimateMapping(_stationService).Register(config);
    config.Compile();

    return config;
  }
}
