using ErrorOr;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;

namespace GastronomyApp.Core.Services;

public sealed class CatalogItemRecipeService
{
  private readonly IIngredientRepository _ingredientRepository;
  private readonly ICatalogItemRepository _itemRepository;
  private readonly ICatalogItemIngredientRepository _repository;

  public CatalogItemRecipeService(ICatalogItemIngredientRepository repository, ICatalogItemRepository itemRepository, IIngredientRepository ingredientRepository)
  {
    _repository = repository;
    _itemRepository = itemRepository;
    _ingredientRepository = ingredientRepository;
  }

  public async Task<ErrorOr<CatalogItemIngredient>> SetAmountAsync(Guid itemId, Guid ingredientId, double? amount, CancellationToken cancellationToken)
  {
    if (await _itemRepository.FindByIdAsync(itemId, cancellationToken) is null)
      return Refusal.Ingredients.ItemNotFound(itemId);

    if (await _ingredientRepository.FindByIdAsync(ingredientId, cancellationToken) is null)
      return Refusal.Ingredients.IngredientNotFound(ingredientId);

    if (amount is not { } usedPerUnit || !double.IsFinite(usedPerUnit) || usedPerUnit <= 0)
      return Refusal.Ingredients.AmountInvalid(amount ?? double.NaN);

    var recipeLine = await _repository.FindAsync(itemId, ingredientId, cancellationToken);

    if (recipeLine is null)
    {
      recipeLine = new()
      {
        Id = Guid.NewGuid(),
        CatalogItemId = itemId,
        IngredientId = ingredientId,
        Amount = usedPerUnit
      };

      await _repository.AddAsync(recipeLine, cancellationToken);
    }
    else
      recipeLine.Amount = usedPerUnit;

    await _repository.SaveChangesAsync(cancellationToken);

    return recipeLine;
  }

  public async Task<ErrorOr<CatalogItemIngredient>> RemoveAsync(Guid itemId, Guid ingredientId, CancellationToken cancellationToken)
  {
    var recipeLine = await _repository.FindAsync(itemId, ingredientId, cancellationToken);

    if (recipeLine is null)
      return Refusal.Ingredients.RecipeLineNotFound(itemId, ingredientId);

    _repository.Remove(recipeLine);
    await _repository.SaveChangesAsync(cancellationToken);

    return recipeLine;
  }
}
