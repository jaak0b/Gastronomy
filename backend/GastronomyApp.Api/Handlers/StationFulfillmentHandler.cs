using ErrorOr;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Values;
using GastronomyApp.Contracts.Stations;
using GastronomyApp.Core.Services;
using MapsterMapper;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Handlers;

public sealed class StationFulfillmentHandler
{
  private readonly StationQueueChangeService _changeService;
  private readonly IMapper _mapper;
  private readonly ResultEnvelope _resultEnvelope;

  public StationFulfillmentHandler(StationQueueChangeService changeService, IMapper mapper, ResultEnvelope resultEnvelope)
  {
    _changeService = changeService;
    _mapper = mapper;
    _resultEnvelope = resultEnvelope;
  }

  public async Task<IResult> FulfillAsync(StationItemSelectionRequest request, StationDeviceCaller caller, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(caller);

    return await _changeService.FulfillAsync(request.OrderItemIds ?? [], caller.StationId, cancellationToken).Match(station => Results.Ok(_mapper.Map<StationQueueView>(station)), _resultEnvelope.Refuse);
  }

  public async Task<IResult> UnfulfillAsync(StationItemSelectionRequest request, StationDeviceCaller caller, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(caller);

    return await _changeService.UnfulfillAsync(request.OrderItemIds ?? [], caller.StationId, cancellationToken).Match(station => Results.Ok(_mapper.Map<StationQueueView>(station)), _resultEnvelope.Refuse);
  }

  public async Task<IResult> HideAsync(Guid stationOrderId, StationDeviceCaller caller, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(caller);

    return await _changeService.HideFromAsItComesQueueAsync(stationOrderId, caller.StationId, cancellationToken).Match(station => Results.Ok(_mapper.Map<StationQueueView>(station)), _resultEnvelope.Refuse);
  }
}
