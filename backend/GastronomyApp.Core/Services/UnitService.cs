using ErrorOr;
using GastronomyApp.Contracts.Admin.Units;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Core.Services;

public sealed class UnitService
{
  private readonly IGastronomyDatabase _database;
  private readonly TypeAdapterConfig _mapping;

  public UnitService(IGastronomyDatabase database, TypeAdapterConfig mapping)
  {
    _database = database;
    _mapping = mapping;
  }

  public async Task<AdminUnitListView> ListAsync(CancellationToken cancellationToken)
  {
    var units = await _database.Units.AsNoTracking().OrderBy(unit => unit.Name).ProjectToType<AdminUnitView>(_mapping).ToListAsync(cancellationToken);

    return new(units);
  }

  public async Task<ErrorOr<AdminUnitView>> CreateAsync(SaveUnitRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    var nameCheck = await CheckNameIsFreeAsync(request.Name!, null, cancellationToken);

    if (nameCheck.IsError)
      return nameCheck.Errors;

    Unit created = new()
                   {
                     Id = Guid.NewGuid(),
                     Name = request.Name!,
                     IsActive = true
                   };

    _database.Units.Add(created);
    await _database.SaveChangesAsync(cancellationToken);

    return created.Adapt<AdminUnitView>(_mapping);
  }

  public async Task<ErrorOr<SavedUnitView>> UpdateAsync(Guid unitId, SaveUnitRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    var unit = await _database.Units.FirstOrDefaultAsync(row => row.Id == unitId, cancellationToken);

    if (unit is null)
      return Refusal.Units.UnitNotFound(unitId);

    var nameCheck = await CheckNameIsFreeAsync(request.Name!, unitId, cancellationToken);

    if (nameCheck.IsError)
      return nameCheck.Errors;

    unit.Name = request.Name!;
    await _database.SaveChangesAsync(cancellationToken);

    return unit.Adapt<SavedUnitView>(_mapping);
  }

  public async Task<ErrorOr<SavedUnitView>> DeactivateAsync(Guid unitId, CancellationToken cancellationToken)
  {
    var unit = await _database.Units.FirstOrDefaultAsync(row => row.Id == unitId, cancellationToken);

    if (unit is null)
      return Refusal.Units.UnitNotFound(unitId);

    var ingredientNames = await _database.Ingredients.Where(ingredient => ingredient.UnitId == unitId).Select(ingredient => ingredient.Name).OrderBy(ingredientName => ingredientName).ToListAsync(cancellationToken);

    if (ingredientNames.Count > 0)
      return Refusal.Units.UsedByIngredients(unitId, ingredientNames);

    unit.IsActive = false;
    await _database.SaveChangesAsync(cancellationToken);

    return unit.Adapt<SavedUnitView>(_mapping);
  }

  private async Task<ErrorOr<Success>> CheckNameIsFreeAsync(string name, Guid? ownUnitId, CancellationToken cancellationToken)
  {
    var others = await _database.Units.AsNoTracking().Where(other => other.Id != ownUnitId).ToListAsync(cancellationToken);
    var namesake = others.FirstOrDefault(other => string.Equals(other.Name, name, StringComparison.OrdinalIgnoreCase));

    if (namesake is { IsActive: false })
      return Refusal.Units.NameBelongsToDeactivatedUnit(namesake.Name);

    if (namesake is not null)
      return Refusal.Units.NameTaken(name);

    return Result.Success;
  }
}
