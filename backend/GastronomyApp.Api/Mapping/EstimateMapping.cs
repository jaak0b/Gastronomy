using GastronomyApp.Contracts.Orders;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using Mapster;

namespace GastronomyApp.Api.Mapping;

public sealed class EstimateMapping : IMappingRegistration
{
  private readonly StationService _stationService;

  public EstimateMapping(StationService stationService)
  {
    _stationService = stationService;
  }

  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<ItemStationAssignment, ItemEstimateView>().Map(view => view.ReadyInMinutes, assignment => _stationService.ReadyInMinutesOf(assignment));
    config.NewConfig<StationQuote, StationQuoteView>();
  }
}
