using ErrorOr;
using GastronomyApp.Api.Answers;
using GastronomyApp.Contracts.Orders;
using GastronomyApp.Core.Services;
using MapsterMapper;

namespace GastronomyApp.Api.Handlers;

public sealed class EstimateHandler
{
  private readonly EstimateService _estimateService;
  private readonly IMapper _mapper;

  public EstimateHandler(EstimateService estimateService, IMapper mapper)
  {
    _estimateService = estimateService;
    _mapper = mapper;
  }

  public async Task<ApiAnswer<IReadOnlyList<ItemEstimateView>>> ListAsync(CancellationToken cancellationToken)
  {
    return new(_mapper.Map<IReadOnlyList<ItemEstimateView>>(await _estimateService.ReadTimedAssignmentsAsync(cancellationToken)).ToErrorOr());
  }

  public async Task<ApiAnswer<EstimateQuoteView>> QuoteAsync(EstimateQuoteRequest request, CancellationToken cancellationToken)
  {
    return (await _estimateService.QuoteAsync(request, cancellationToken)).Then(quotes => new EstimateQuoteView(_mapper.Map<IReadOnlyList<StationQuoteView>>(quotes)));
  }
}
