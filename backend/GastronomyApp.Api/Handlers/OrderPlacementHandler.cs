using ErrorOr;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Values;
using GastronomyApp.Contracts.Orders;
using GastronomyApp.Core.Services;
using MapsterMapper;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Handlers;

public sealed class OrderPlacementHandler
{
  private readonly OrderAcceptanceService _acceptanceService;
  private readonly IMapper _mapper;
  private readonly ResultEnvelope _resultEnvelope;

  public OrderPlacementHandler(OrderAcceptanceService acceptanceService, ResultEnvelope resultEnvelope, IMapper mapper)
  {
    _acceptanceService = acceptanceService;
    _resultEnvelope = resultEnvelope;
    _mapper = mapper;
  }

  public async Task<IResult> PlaceAsync(PlaceOrderRequest request, StaffDeviceCaller caller, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(caller);

    return await _acceptanceService.AcceptAsync(request, caller.StaffMemberId, cancellationToken).Match(order => Results.Json(_mapper.Map<PlacedOrderView>(order), statusCode: StatusCodes.Status201Created), _resultEnvelope.Refuse);
  }
}
