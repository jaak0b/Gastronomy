using ErrorOr;
using GastronomyApp.Api.Answers;
using GastronomyApp.Contracts.Admin.Ingredients;
using GastronomyApp.Core.Services;
using MapsterMapper;

namespace GastronomyApp.Api.Handlers;

public sealed class AdminFestivalStockHandler
{
  private readonly IMapper _mapper;
  private readonly IngredientStockService _service;

  public AdminFestivalStockHandler(IngredientStockService service, IMapper mapper)
  {
    _service = service;
    _mapper = mapper;
  }

  public async Task<ApiAnswer<AdminFestivalIngredientListView>> ListAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    return await _service.ListAtFestivalAsync(festivalId, cancellationToken).Then(levels => new AdminFestivalIngredientListView(_mapper.Map<IReadOnlyList<AdminFestivalIngredientView>>(levels)));
  }

  public async Task<ApiAnswer<SavedFestivalIngredientView>> SetAvailableAmountAsync(Guid festivalId, Guid ingredientId, SaveFestivalIngredientRequest request, CancellationToken cancellationToken)
  {
    return await _service.SetAvailableAmountAsync(festivalId, ingredientId, request.AvailableAmount, cancellationToken).Then(stock => new SavedFestivalIngredientView(stock.IngredientId));
  }
}
