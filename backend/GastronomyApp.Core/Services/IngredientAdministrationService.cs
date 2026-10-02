using ErrorOr;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;

namespace GastronomyApp.Core.Services;

public sealed class IngredientAdministrationService
{
  private readonly IFestivalRepository _festivalRepository;
  private readonly IIngredientRepository _repository;

  public IngredientAdministrationService(IIngredientRepository repository, IFestivalRepository festivalRepository)
  {
    _repository = repository;
    _festivalRepository = festivalRepository;
  }

  public Task<IReadOnlyList<Ingredient>> ListAsync(CancellationToken cancellationToken)
  {
    return _repository.FindAllOrderedByNameAsync(cancellationToken);
  }

  public async Task<ErrorOr<Ingredient>> CreateAsync(string? name, IngredientUnit? unit, CancellationToken cancellationToken)
  {
    if (unit is not (>= IngredientUnit.Piece and <= IngredientUnit.Millilitre))
      return Refusal.Ingredients.UnitUndefined(unit);

    IReadOnlyList<Ingredient> ingredients = await _repository.FindAllOrderedByNameAsync(cancellationToken);

    if (ingredients.Any(ingredient => string.Equals(ingredient.Name, name, StringComparison.OrdinalIgnoreCase)))
      return Refusal.Ingredients.NameTaken(name!);

    Ingredient created = new()
    {
      Id = Guid.NewGuid(),
      Name = name!,
      Unit = unit.Value,
      IsActive = true
    };

    IReadOnlyCollection<Festival> festivals = await _festivalRepository.FindAllAsync(cancellationToken);

    foreach (var festival in festivals)
      created.FestivalIngredients.Add(new()
                                      {
                                        Id = Guid.NewGuid(),
                                        FestivalId = festival.Id,
                                        IngredientId = created.Id,
                                        AvailableAmount = null
                                      });

    await _repository.AddAsync(created, cancellationToken);
    await _repository.SaveChangesAsync(cancellationToken);

    return created;
  }

  public async Task<ErrorOr<Ingredient>> UpdateAsync(Guid ingredientId, string? name, IngredientUnit? unit, CancellationToken cancellationToken)
  {
    var ingredient = await _repository.FindByIdAsync(ingredientId, cancellationToken);

    if (ingredient is null)
      return Refusal.Ingredients.IngredientNotFound(ingredientId);

    if (unit is not (>= IngredientUnit.Piece and <= IngredientUnit.Millilitre))
      return Refusal.Ingredients.UnitUndefined(unit);

    IReadOnlyList<Ingredient> ingredients = await _repository.FindAllOrderedByNameAsync(cancellationToken);

    if (ingredients.Any(other => other.Id != ingredientId && string.Equals(other.Name, name, StringComparison.OrdinalIgnoreCase)))
      return Refusal.Ingredients.NameTaken(name!);

    ingredient.Name = name!;
    ingredient.Unit = unit.Value;
    await _repository.SaveChangesAsync(cancellationToken);

    return ingredient;
  }

  public async Task<ErrorOr<Ingredient>> ActivateAsync(Guid ingredientId, CancellationToken cancellationToken)
  {
    var ingredient = await _repository.FindByIdAsync(ingredientId, cancellationToken);

    if (ingredient is null)
      return Refusal.Ingredients.IngredientNotFound(ingredientId);

    ingredient.IsActive = true;
    await _repository.SaveChangesAsync(cancellationToken);

    return ingredient;
  }

  public async Task<ErrorOr<Ingredient>> DeactivateAsync(Guid ingredientId, CancellationToken cancellationToken)
  {
    var ingredient = await _repository.FindByIdAsync(ingredientId, cancellationToken);

    if (ingredient is null)
      return Refusal.Ingredients.IngredientNotFound(ingredientId);

    ingredient.IsActive = false;
    await _repository.SaveChangesAsync(cancellationToken);

    return ingredient;
  }
}
