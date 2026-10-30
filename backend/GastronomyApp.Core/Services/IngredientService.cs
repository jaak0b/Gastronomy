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

    var unit = await FindUnitAsync(request.UnitId, cancellationToken);

    if (unit is null)
      return Refusal.Ingredients.UnitMissingOrUnknown(request.UnitId);

    Ingredient created = new()
                         {
                           Id = Guid.NewGuid(),
                           Name = request.Name!,
                           UnitId = unit.Id,
                           Unit = unit
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
      return Refusal.Ingredients.UnitMissingOrUnknown(request.UnitId);

    Unit? unit = null;

    if (requestedUnitId != ingredient.UnitId)
    {
      unit = await FindUnitAsync(requestedUnitId, cancellationToken);

      if (unit is null)
        return Refusal.Ingredients.UnitMissingOrUnknown(requestedUnitId);
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

  public async Task<ErrorOr<Success>> DeleteAsync(Guid ingredientId, CancellationToken cancellationToken)
  {
    var ingredient = await _database.Ingredients.Include(row => row.FestivalIngredients).FirstOrDefaultAsync(row => row.Id == ingredientId, cancellationToken);

    if (ingredient is null)
      return Refusal.Ingredients.IngredientNotFound(ingredientId);

    var itemNames = await _database.Ingredients.Where(row => row.Id == ingredientId).Select(row => row.UsedByItemNames().ToList()).SingleAsync(cancellationToken);

    if (itemNames.Count > 0)
      return Refusal.Ingredients.UsedByItems(ingredientId, itemNames);

    _database.FestivalIngredients.RemoveRange(ingredient.FestivalIngredients);
    _database.Ingredients.Remove(ingredient);
    await _database.SaveChangesAsync(cancellationToken);

    return Result.Success;
  }

  private async Task<Unit?> FindUnitAsync(Guid? unitId, CancellationToken cancellationToken)
  {
    return await _database.Units.FirstOrDefaultAsync(unit => unit.Id == unitId, cancellationToken);
  }

  private async Task<ErrorOr<Success>> CheckNameIsFreeAsync(string name, Guid ingredientId, CancellationToken cancellationToken)
  {
    if (await _database.Ingredients.AsNoTracking().Namesakes(name, ingredientId).AnyAsync(cancellationToken))
      return Refusal.Ingredients.NameTaken(name);

    return Result.Success;
  }
}
