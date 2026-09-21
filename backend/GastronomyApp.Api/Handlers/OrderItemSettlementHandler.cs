using ErrorOr;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Values;
using GastronomyApp.Contracts.OpenItems;
using GastronomyApp.Core.Services;
using MapsterMapper;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Handlers;

public sealed class OrderItemSettlementHandler
{
  private readonly IMapper _mapper;
  private readonly ResultEnvelope _resultEnvelope;
  private readonly OrderItemSettlementService _settlementService;

  public OrderItemSettlementHandler(OrderItemSettlementService settlementService, ResultEnvelope resultEnvelope, IMapper mapper)
  {
    _settlementService = settlementService;
    _resultEnvelope = resultEnvelope;
    _mapper = mapper;
  }

  public async Task<IResult> SettleAsync(SettleItemsRequest request, StaffDeviceCaller caller, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(caller);

    return await _settlementService.SettleAsync(request.Lines ?? [], caller.StaffMemberId, cancellationToken).Match(settlement => Results.Ok(_mapper.Map<SettlementView>(settlement)), _resultEnvelope.Refuse);
  }
}
