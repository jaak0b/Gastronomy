using ErrorOr;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;

namespace GastronomyApp.Core.Services;

public sealed class IngredientAdministrationService
{
  private readonly IFestivalRepository _festivalRepository;
  private readonly IngredientService _ingredientService;
  private readonly IIngredientRepository _repository;
  private readonly IUnitRepository _unitRepository;

  public IngredientAdministrationService(IIngredientRepository repository, IFestivalRepository festivalRepository, IngredientService ingredientService, IUnitRepository unitRepository)
  {
    _repository = repository;
    _festivalRepository = festivalRepository;
    _ingredientService = ingredientService;
    _unitRepository = unitRepository;
  }

  public Task<IReadOnlyList<Ingredient>> ListAsync(CancellationToken cancellationToken)
  {
    return _repository.FindAllWithRecipeLinesOrderedByNameAsync(cancellationToken);
  }

  public async Task<ErrorOr<Ingredient>> CreateAsync(string? name, Guid? unitId, CancellationToken cancellationToken)
  {
    if (unitId is not { } requestedUnitId)
      return Refusal.Ingredients.UnitUnavailable(unitId);

    var unit = await _unitRepository.FindByIdAsync(requestedUnitId, cancellationToken);

    if (unit is not { IsActive: true })
      return Refusal.Ingredients.UnitUnavailable(unitId);

    IReadOnlyList<Ingredient> ingredients = await _repository.FindAllOrderedByNameAsync(cancellationToken);
    var namesake = ingredients.FirstOrDefault(ingredient => string.Equals(ingredient.Name, name, StringComparison.OrdinalIgnoreCase));

    if (namesake is { IsActive: false })
      return Refusal.Ingredients.NameBelongsToDeactivatedIngredient(namesake.Name);

    if (namesake is not null)
      return Refusal.Ingredients.NameTaken(name!);

    Ingredient created = new()
                         {
                           Id = Guid.NewGuid(),
                           Name = name!,
                           UnitId = unit.Id,
                           Unit = unit,
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

  public async Task<ErrorOr<Ingredient>> UpdateAsync(Guid ingredientId, string? name, Guid? unitId, CancellationToken cancellationToken)
  {
    var ingredient = await _repository.FindByIdAsync(ingredientId, cancellationToken);

    if (ingredient is null)
      return Refusal.Ingredients.IngredientNotFound(ingredientId);

    if (unitId is not { } requestedUnitId)
      return Refusal.Ingredients.UnitUnavailable(unitId);

    Unit? unit = null;

    if (requestedUnitId != ingredient.UnitId)
    {
      unit = await _unitRepository.FindByIdAsync(requestedUnitId, cancellationToken);

      if (unit is not { IsActive: true })
        return Refusal.Ingredients.UnitUnavailable(unitId);
    }

    IReadOnlyList<Ingredient> ingredients = await _repository.FindAllOrderedByNameAsync(cancellationToken);
    var namesake = ingredients.FirstOrDefault(other => other.Id != ingredientId && string.Equals(other.Name, name, StringComparison.OrdinalIgnoreCase));

    if (namesake is { IsActive: false })
      return Refusal.Ingredients.NameBelongsToDeactivatedIngredient(namesake.Name);

    if (namesake is not null)
      return Refusal.Ingredients.NameTaken(name!);

    ingredient.Name = name!;

    if (unit is not null)
    {
      ingredient.UnitId = unit.Id;
      ingredient.Unit = unit;
    }

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
    var ingredient = await _repository.FindWithRecipeLinesAsync(ingredientId, cancellationToken);

    if (ingredient is null)
      return Refusal.Ingredients.IngredientNotFound(ingredientId);

    IReadOnlyList<string> activeItemNames = _ingredientService.ActiveItemNamesOf(ingredient);

    if (activeItemNames.Count > 0)
      return Refusal.Ingredients.UsedByActiveItems(ingredientId, activeItemNames);

    ingredient.IsActive = false;
    await _repository.SaveChangesAsync(cancellationToken);

    return ingredient;
  }
}
