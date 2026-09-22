using ErrorOr;
using GastronomyApp.Api.Answers;
using GastronomyApp.Api.Values;
using GastronomyApp.Contracts.OpenItems;
using GastronomyApp.Core.Services;
using MapsterMapper;

namespace GastronomyApp.Api.Handlers;

public sealed class OrderItemSettlementHandler
{
  private readonly IMapper _mapper;
  private readonly OrderItemSettlementService _settlementService;

  public OrderItemSettlementHandler(OrderItemSettlementService settlementService, IMapper mapper)
  {
    _settlementService = settlementService;
    _mapper = mapper;
  }

  public async Task<ApiAnswer<SettlementView>> SettleAsync(SettleItemsRequest request, StaffDeviceCaller caller, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(caller);

    return await _settlementService.SettleAsync(request.Lines ?? [], caller.StaffMemberId, cancellationToken).Then(_mapper.Map<SettlementView>);
  }
}
