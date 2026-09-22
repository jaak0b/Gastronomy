using ErrorOr;
using GastronomyApp.Api.Answers;
using GastronomyApp.Api.Values;
using GastronomyApp.Contracts.Orders;
using GastronomyApp.Core.Services;
using MapsterMapper;

namespace GastronomyApp.Api.Handlers;

public sealed class OrderPlacementHandler
{
  private readonly OrderAcceptanceService _acceptanceService;
  private readonly IMapper _mapper;

  public OrderPlacementHandler(OrderAcceptanceService acceptanceService, IMapper mapper)
  {
    _acceptanceService = acceptanceService;
    _mapper = mapper;
  }

  public async Task<CreatedAnswer<PlacedOrderView>> PlaceAsync(PlaceOrderRequest request, StaffDeviceCaller caller, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(caller);

    return await _acceptanceService.AcceptAsync(request, caller.StaffMemberId, cancellationToken).Then(_mapper.Map<PlacedOrderView>);
  }
}
