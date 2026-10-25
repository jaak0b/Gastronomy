using ErrorOr;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;

namespace GastronomyApp.Core.Services;

public sealed class UnitAdministrationService
{
  private readonly IUnitRepository _repository;
  private readonly UnitService _unitService;

  public UnitAdministrationService(IUnitRepository repository, UnitService unitService)
  {
    _repository = repository;
    _unitService = unitService;
  }

  public Task<IReadOnlyList<Unit>> ListAsync(CancellationToken cancellationToken)
  {
    return _repository.FindAllWithIngredientsOrderedByNameAsync(cancellationToken);
  }

  public async Task<ErrorOr<Unit>> CreateAsync(string? name, CancellationToken cancellationToken)
  {
    IReadOnlyList<Unit> units = await _repository.FindAllOrderedByNameAsync(cancellationToken);
    var namesake = units.FirstOrDefault(unit => string.Equals(unit.Name, name, StringComparison.OrdinalIgnoreCase));

    if (namesake is { IsActive: false })
      return Refusal.Units.NameBelongsToDeactivatedUnit(namesake.Name);

    if (namesake is not null)
      return Refusal.Units.NameTaken(name!);

    Unit created = new()
                   {
                     Id = Guid.NewGuid(),
                     Name = name!,
                     IsActive = true
                   };

    await _repository.AddAsync(created, cancellationToken);
    await _repository.SaveChangesAsync(cancellationToken);

    return created;
  }

  public async Task<ErrorOr<Unit>> UpdateAsync(Guid unitId, string? name, CancellationToken cancellationToken)
  {
    var unit = await _repository.FindByIdAsync(unitId, cancellationToken);

    if (unit is null)
      return Refusal.Units.UnitNotFound(unitId);

    IReadOnlyList<Unit> units = await _repository.FindAllOrderedByNameAsync(cancellationToken);
    var namesake = units.FirstOrDefault(other => other.Id != unitId && string.Equals(other.Name, name, StringComparison.OrdinalIgnoreCase));

    if (namesake is { IsActive: false })
      return Refusal.Units.NameBelongsToDeactivatedUnit(namesake.Name);

    if (namesake is not null)
      return Refusal.Units.NameTaken(name!);

    unit.Name = name!;
    await _repository.SaveChangesAsync(cancellationToken);

    return unit;
  }

  public async Task<ErrorOr<Unit>> ActivateAsync(Guid unitId, CancellationToken cancellationToken)
  {
    var unit = await _repository.FindByIdAsync(unitId, cancellationToken);

    if (unit is null)
      return Refusal.Units.UnitNotFound(unitId);

    unit.IsActive = true;
    await _repository.SaveChangesAsync(cancellationToken);

    return unit;
  }

  public async Task<ErrorOr<Unit>> DeactivateAsync(Guid unitId, CancellationToken cancellationToken)
  {
    var unit = await _repository.FindWithIngredientsAsync(unitId, cancellationToken);

    if (unit is null)
      return Refusal.Units.UnitNotFound(unitId);

    IReadOnlyList<string> ingredientNames = _unitService.IngredientNamesOf(unit);

    if (ingredientNames.Count > 0)
      return Refusal.Units.UsedByIngredients(unitId, ingredientNames);

    unit.IsActive = false;
    await _repository.SaveChangesAsync(cancellationToken);

    return unit;
  }
}
