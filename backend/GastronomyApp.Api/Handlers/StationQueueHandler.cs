using ErrorOr;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Values;
using GastronomyApp.Contracts.Stations;
using GastronomyApp.Core.Services;
using MapsterMapper;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Handlers;

public sealed class StationQueueHandler
{
  private readonly IMapper _mapper;
  private readonly StationQueueService _queueService;
  private readonly ResultEnvelope _resultEnvelope;

  public StationQueueHandler(StationQueueService queueService, IMapper mapper, ResultEnvelope resultEnvelope)
  {
    _queueService = queueService;
    _mapper = mapper;
    _resultEnvelope = resultEnvelope;
  }

  public async Task<IResult> ListQueueAsync(StationDeviceCaller caller, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(caller);

    return await _queueService.ReadQueueAsync(caller.StationId, cancellationToken)
                              .Match(station => Results.Ok(_mapper.Map<StationQueueView>(station)), _resultEnvelope.Refuse);
  }

  public async Task<IResult> ListFulfilledAsync(StationDeviceCaller caller, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(caller);

    return await _queueService.ReadFulfilledAsync(caller.StationId, cancellationToken)
                              .Match(fulfilled => Results.Ok(new StationFulfilledView(_mapper.Map<IReadOnlyList<StationOrderQueueView>>(fulfilled))), _resultEnvelope.Refuse);
  }
}
