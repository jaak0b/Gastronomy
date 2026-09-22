using ErrorOr;
using GastronomyApp.Api.Answers;
using GastronomyApp.Api.Values;
using GastronomyApp.Contracts.Stations;
using GastronomyApp.Core.Services;
using MapsterMapper;

namespace GastronomyApp.Api.Handlers;

public sealed class StationQueueHandler
{
  private readonly IMapper _mapper;
  private readonly StationQueueService _queueService;

  public StationQueueHandler(StationQueueService queueService, IMapper mapper)
  {
    _queueService = queueService;
    _mapper = mapper;
  }

  public async Task<ApiAnswer<StationQueueView>> ListQueueAsync(StationDeviceCaller caller, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(caller);

    return await _queueService.ReadQueueAsync(caller.StationId, cancellationToken).Then(_mapper.Map<StationQueueView>);
  }

  public async Task<ApiAnswer<StationFulfilledView>> ListFulfilledAsync(StationDeviceCaller caller, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(caller);

    return await _queueService.ReadFulfilledAsync(caller.StationId, cancellationToken).Then(fulfilled => new StationFulfilledView(_mapper.Map<IReadOnlyList<StationOrderQueueView>>(fulfilled)));
  }
}
