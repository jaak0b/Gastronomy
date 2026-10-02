using ErrorOr;
using GastronomyApp.Api.Answers;
using GastronomyApp.Contracts.Admin.Catalog;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Api.Handlers;

public sealed class AdminItemRecipeHandler
{
  private readonly CatalogItemRecipeService _service;

  public AdminItemRecipeHandler(CatalogItemRecipeService service)
  {
    _service = service;
  }

  public async Task<ApiAnswer<SavedItemView>> SetAmountAsync(Guid itemId, Guid ingredientId, SaveItemIngredientRequest request, CancellationToken cancellationToken)
  {
    return await _service.SetAmountAsync(itemId, ingredientId, request.Amount, cancellationToken).Then(recipeLine => new SavedItemView(recipeLine.CatalogItemId));
  }

  public async Task<NoContentAnswer> RemoveAsync(Guid itemId, Guid ingredientId, CancellationToken cancellationToken)
  {
    return await _service.RemoveAsync(itemId, ingredientId, cancellationToken).Then(recipeLine => Result.Success);
  }
}
