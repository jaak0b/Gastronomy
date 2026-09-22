using ErrorOr;
using GastronomyApp.Api.Answers;
using GastronomyApp.Api.Values;
using GastronomyApp.Contracts.Stations;
using GastronomyApp.Core.Services;
using MapsterMapper;

namespace GastronomyApp.Api.Handlers;

public sealed class StationFulfillmentHandler
{
  private readonly StationQueueChangeService _changeService;
  private readonly IMapper _mapper;

  public StationFulfillmentHandler(StationQueueChangeService changeService, IMapper mapper)
  {
    _changeService = changeService;
    _mapper = mapper;
  }

  public async Task<ApiAnswer<StationQueueView>> FulfillAsync(StationItemSelectionRequest request, StationDeviceCaller caller, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(caller);

    return await _changeService.FulfillAsync(request.OrderItemIds ?? [], caller.StationId, cancellationToken).Then(_mapper.Map<StationQueueView>);
  }

  public async Task<ApiAnswer<StationQueueView>> UnfulfillAsync(StationItemSelectionRequest request, StationDeviceCaller caller, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(caller);

    return await _changeService.UnfulfillAsync(request.OrderItemIds ?? [], caller.StationId, cancellationToken).Then(_mapper.Map<StationQueueView>);
  }

  public async Task<ApiAnswer<StationQueueView>> HideAsync(Guid stationOrderId, StationDeviceCaller caller, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(caller);

    return await _changeService.HideFromAsItComesQueueAsync(stationOrderId, caller.StationId, cancellationToken).Then(_mapper.Map<StationQueueView>);
  }
}
