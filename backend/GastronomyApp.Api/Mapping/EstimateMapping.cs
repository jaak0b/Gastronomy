using GastronomyApp.Contracts.Orders;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using Mapster;

namespace GastronomyApp.Api.Mapping;

public sealed class EstimateMapping : IMappingRegistration
{
  private readonly StationWaitingTimes _stationWaitingTimes;

  public EstimateMapping(StationWaitingTimes stationWaitingTimes)
  {
    _stationWaitingTimes = stationWaitingTimes;
  }

  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<ItemStationAssignment, ItemEstimateView>().Map(view => view.ReadyInMinutes, assignment => _stationWaitingTimes.ReadyInMinutesOf(assignment));
    config.NewConfig<StationQuote, StationQuoteView>();
  }
}
