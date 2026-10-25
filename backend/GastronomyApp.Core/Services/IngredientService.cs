using ErrorOr;
using GastronomyApp.Contracts.Admin.Ingredients;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Queries;
using GastronomyApp.Core.Refusals;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Core.Services;

public sealed class IngredientService
{
  private readonly IGastronomyDatabase _database;
  private readonly TypeAdapterConfig _mapping;

  public IngredientService(IGastronomyDatabase database, TypeAdapterConfig mapping)
  {
    _database = database;
    _mapping = mapping;
  }

  public async Task<AdminIngredientListView> ListAsync(CancellationToken cancellationToken)
  {
    var ingredients = await _database.Ingredients.AsNoTracking().OrderBy(ingredient => ingredient.Name).ProjectToType<AdminIngredientView>(_mapping).ToListAsync(cancellationToken);

    return new(ingredients);
  }

  public async Task<ErrorOr<AdminIngredientView>> CreateAsync(SaveIngredientRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    var unit = await FindActiveUnitAsync(request.UnitId, cancellationToken);

    if (unit is null)
      return Refusal.Ingredients.UnitUnavailable(request.UnitId);

    Ingredient created = new()
                         {
                           Id = Guid.NewGuid(),
                           Name = request.Name!,
                           UnitId = unit.Id,
                           Unit = unit,
                           IsActive = true
                         };

    var nameCheck = await CheckNameIsFreeAsync(created.Name, created.Id, cancellationToken);

    if (nameCheck.IsError)
      return nameCheck.Errors;

    var festivalIds = await _database.Festivals.AsNoTracking().Select(festival => festival.Id).ToListAsync(cancellationToken);

    foreach (var festivalId in festivalIds)
      created.FestivalIngredients.Add(new()
                                      {
                                        Id = Guid.NewGuid(),
                                        FestivalId = festivalId,
                                        IngredientId = created.Id,
                                        AvailableAmount = null
                                      });

    _database.Ingredients.Add(created);
    await _database.SaveChangesAsync(cancellationToken);

    return created.Adapt<AdminIngredientView>(_mapping);
  }

  public async Task<ErrorOr<SavedIngredientView>> UpdateAsync(Guid ingredientId, SaveIngredientRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    var ingredient = await _database.Ingredients.FirstOrDefaultAsync(row => row.Id == ingredientId, cancellationToken);

    if (ingredient is null)
      return Refusal.Ingredients.IngredientNotFound(ingredientId);

    if (request.UnitId is not { } requestedUnitId)
      return Refusal.Ingredients.UnitUnavailable(request.UnitId);

    Unit? unit = null;

    if (requestedUnitId != ingredient.UnitId)
    {
      unit = await FindActiveUnitAsync(requestedUnitId, cancellationToken);

      if (unit is null)
        return Refusal.Ingredients.UnitUnavailable(requestedUnitId);
    }

    var nameCheck = await CheckNameIsFreeAsync(request.Name!, ingredientId, cancellationToken);

    if (nameCheck.IsError)
      return nameCheck.Errors;

    ingredient.Name = request.Name!;

    if (unit is not null)
    {
      ingredient.UnitId = unit.Id;
      ingredient.Unit = unit;
    }

    await _database.SaveChangesAsync(cancellationToken);

    return ingredient.Adapt<SavedIngredientView>(_mapping);
  }

  public async Task<ErrorOr<SavedIngredientView>> DeactivateAsync(Guid ingredientId, CancellationToken cancellationToken)
  {
    var ingredient = await _database.Ingredients.FirstOrDefaultAsync(row => row.Id == ingredientId, cancellationToken);

    if (ingredient is null)
      return Refusal.Ingredients.IngredientNotFound(ingredientId);

    var activeItemNames = await _database.CatalogItemIngredients.Where(recipeLine => recipeLine.IngredientId == ingredientId).Select(recipeLine => recipeLine.CatalogItem).Active().Select(item => item.Name).OrderBy(itemName => itemName).ToListAsync(cancellationToken);

    if (activeItemNames.Count > 0)
      return Refusal.Ingredients.UsedByActiveItems(ingredientId, activeItemNames);

    ingredient.IsActive = false;
    await _database.SaveChangesAsync(cancellationToken);

    return ingredient.Adapt<SavedIngredientView>(_mapping);
  }

  private async Task<Unit?> FindActiveUnitAsync(Guid? unitId, CancellationToken cancellationToken)
  {
    return await _database.Units.Active().FirstOrDefaultAsync(unit => unit.Id == unitId, cancellationToken);
  }

  private async Task<ErrorOr<Success>> CheckNameIsFreeAsync(string name, Guid ingredientId, CancellationToken cancellationToken)
  {
    var others = await _database.Ingredients.AsNoTracking().Where(other => other.Id != ingredientId).ToListAsync(cancellationToken);
    var namesake = others.FirstOrDefault(other => string.Equals(other.Name, name, StringComparison.OrdinalIgnoreCase));

    if (namesake is { IsActive: false })
      return Refusal.Ingredients.NameBelongsToDeactivatedIngredient(namesake.Name);

    if (namesake is not null)
      return Refusal.Ingredients.NameTaken(name);

    return Result.Success;
  }
}
